using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;
using PasheCPE.Firma;
using PasheCPE.Mapeo;
using PasheCPE.Tiempo;
using PasheCPE.Transporte;

namespace PasheCPE.Servicios;

/// <summary>
/// Generación del resumen diario (TRD §6, PRD RF-18) y de la comunicación de baja. Bajo el bloqueo del día y del tipo
/// (<see cref="AsignadorCorrelativos"/>): valida el modelo y el historial de los documentos → asigna el correlativo →
/// XML validado (DATA) → firma (FIRMA) → ZIP (ENVIO) → historial (LOG) en <c>Generado</c> con los documentos informados.
/// El resumen se divide en lotes de 500: un error del modelo detiene todo; un lote con errores no se escribe y no impide
/// los demás. La vía «desde FIRMA» arma las líneas leyendo el XML firmado de cada documento. La baja es una sola RA y
/// marca en el historial de cada documento la RA que lo informa (<see cref="HistorialCpe.Baja"/>).
/// </summary>
internal sealed class GeneracionResumenes(
    GeneradorSummary generador,
    GeneradorVoided generadorBajas,
    LectorBoletaFirmada lectorFirmado,
    AsignadorCorrelativos correlativos,
    BloqueoDocumentos bloqueos,
    ProveedorCertificados certificados,
    FirmadorXml firmador,
    AlmacenCpe almacen,
    RegistroHistorial historial,
    TablaPlazos plazos,
    CatalogoErrores errores,
    RelojCpe reloj,
    ILogger<GeneracionResumenes> registro)
{
    public async Task<IReadOnlyList<ResultadoGeneracion>> GenerarAsync(EmisorCpe emisor, ResumenDiarioCpe resumen, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(resumen);
        var inicio = Stopwatch.GetTimestamp();
        var fechaGeneracion = resumen.FechaGeneracion ?? reloj.HoyLima();
        using var bloqueo = await bloqueos.AdquirirAsync(AsignadorCorrelativos.ClaveBloqueo(emisor.Ruc, ReferenciaCpe.TipoResumenDiario, fechaGeneracion), ct).ConfigureAwait(false);
        var primero = correlativos.Siguiente(emisor.Ruc, ReferenciaCpe.TipoResumenDiario, fechaGeneracion);
        using var actividad = Telemetria.Iniciar(Telemetria.Generar, ReferenciaCpe.Resumen(emisor.Ruc, fechaGeneracion, primero));
        var (encontrados, advertencias) = await generador.ValidarAsync(emisor, resumen, fechaGeneracion, ct).ConfigureAwait(false);
        if (encontrados.Count > 0)
        {
            Telemetria.Concluir(actividad, encontrados.Count);
            return [Resultado(ReferenciaCpe.Resumen(emisor.Ruc, fechaGeneracion, primero), resumen.FechaReferencia, encontrados, advertencias, RutasCpe.Vacio)];
        }

        var certificado = certificados.Obtener(emisor.Certificado);
        if (certificado.Advertencia is { } aviso)
        {
            advertencias = [.. advertencias, aviso];
        }

        var resultados = new List<ResultadoGeneracion>();
        foreach (var lote in generador.Lotes(emisor, resumen, fechaGeneracion, primero))
        {
            IReadOnlyList<DocumentoInformadoCpe> documentos = [.. lote.Lineas.Select(l => new DocumentoInformadoCpe
            {
                Referencia = ReferenciaCpe.Comprobante(emisor.Ruc, l.TipoDocumento, l.Serie, l.Numero, lote.FechaReferencia),
                Estado = l.Estado,
            })];
            var generado = generador.Construir(lote);
            resultados.Add(await EscribirAsync(emisor, lote.FechaReferencia, generado, documentos, certificado, advertencias, inicio, ct).ConfigureAwait(false));
        }

        Telemetria.Concluir(actividad, resultados.Sum(r => r.Errores.Count));
        return resultados;
    }

    /// <summary>Vía «desde FIRMA»: cada línea se arma con el XML firmado del documento, que debe ser de la fecha indicada.</summary>
    public async Task<IReadOnlyList<ResultadoGeneracion>> GenerarAsync(EmisorCpe emisor, DateOnly fechaReferencia, IReadOnlyList<DocumentoResumenCpe> documentos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(documentos);
        var lineas = new List<LineaResumenCpe>();
        var encontrados = new List<ErrorCpe>();
        foreach (var documento in documentos)
        {
            var referencia = ReferenciaCpe.Comprobante(emisor.Ruc, documento.TipoDocumento, documento.Serie, documento.Numero, fechaReferencia);
            var leida = await lectorFirmado.LeerAsync(referencia, documento.Estado, ct).ConfigureAwait(false);
            if (leida.Error is { } error)
            {
                encontrados.Add(error);
            }
            else if (leida.FechaEmision != fechaReferencia)
            {
                encontrados.Add(Error("PCPE-1319", referencia.NombreArchivo, Texto(leida.FechaEmision!.Value), Texto(fechaReferencia)));
            }
            else
            {
                lineas.Add(leida.Linea!);
            }
        }

        if (encontrados.Count > 0)
        {
            var fechaGeneracion = reloj.HoyLima();
            var siguiente = correlativos.Siguiente(emisor.Ruc, ReferenciaCpe.TipoResumenDiario, fechaGeneracion);
            return [Resultado(ReferenciaCpe.Resumen(emisor.Ruc, fechaGeneracion, siguiente), fechaReferencia, encontrados, [], RutasCpe.Vacio)];
        }

        return await GenerarAsync(emisor, new ResumenDiarioCpe { FechaReferencia = fechaReferencia, Lineas = lineas }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Comunicación de baja: una RA con todos los documentos. Al generarla queda anotada en el historial de cada
    /// documento (<see cref="HistorialCpe.Baja"/>), así no se informa en otra RA mientras esta no concluya.
    /// </summary>
    public async Task<ResultadoGeneracion> GenerarBajaAsync(EmisorCpe emisor, ComunicacionBajaCpe baja, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(baja);
        var inicio = Stopwatch.GetTimestamp();
        var fechaGeneracion = baja.FechaGeneracion ?? reloj.HoyLima();
        using var bloqueo = await bloqueos.AdquirirAsync(AsignadorCorrelativos.ClaveBloqueo(emisor.Ruc, ReferenciaCpe.TipoComunicacionBaja, fechaGeneracion), ct).ConfigureAwait(false);
        var referencia = ReferenciaCpe.Baja(emisor.Ruc, fechaGeneracion, correlativos.Siguiente(emisor.Ruc, ReferenciaCpe.TipoComunicacionBaja, fechaGeneracion));
        using var actividad = Telemetria.Iniciar(Telemetria.Generar, referencia);
        var (encontrados, advertencias) = await generadorBajas.ValidarAsync(emisor, baja, fechaGeneracion, ct).ConfigureAwait(false);
        if (encontrados.Count > 0)
        {
            Telemetria.Concluir(actividad, encontrados.Count);
            return Resultado(referencia, baja.FechaReferencia, encontrados, advertencias, RutasCpe.Vacio);
        }

        var certificado = certificados.Obtener(emisor.Certificado);
        if (certificado.Advertencia is { } aviso)
        {
            advertencias = [.. advertencias, aviso];
        }

        IReadOnlyList<DocumentoInformadoCpe> documentos = [.. baja.Documentos.Select(d => new DocumentoInformadoCpe
        {
            Referencia = ReferenciaCpe.Comprobante(emisor.Ruc, d.TipoDocumento, d.Serie, d.Numero, baja.FechaReferencia),
            Estado = EstadoItemResumen.Anulado,
        })];
        var generado = generadorBajas.Construir(new LoteBaja(emisor, referencia, baja.FechaReferencia, baja.Documentos));
        var resultado = await EscribirAsync(emisor, baja.FechaReferencia, generado, documentos, certificado, advertencias, inicio, ct).ConfigureAwait(false);
        if (resultado.Exito)
        {
            foreach (var documento in documentos)
            {
                await historial.RegistrarBajaAsync(documento.Referencia, null, null, referencia, ct).ConfigureAwait(false);
            }
        }

        Telemetria.Concluir(actividad, resultado.Errores.Count);
        return resultado;
    }

    /// <summary>XML validado (DATA) → firma (FIRMA) → ZIP (ENVIO) → historial con los documentos informados.</summary>
    private async Task<ResultadoGeneracion> EscribirAsync(EmisorCpe emisor, DateOnly fechaReferencia, XmlGenerado generado, IReadOnlyList<DocumentoInformadoCpe> documentos, CertificadoObtenido certificado, IReadOnlyList<ObservacionCpe> advertencias, long inicio, CancellationToken ct)
    {
        var referencia = generado.Referencia;
        advertencias = [.. advertencias, .. generado.Advertencias];
        if (!generado.Exito)
        {
            return Resultado(referencia, fechaReferencia, generado.Errores, advertencias, RutasCpe.Vacio);
        }

        var rutas = new RutasCpe { Xml = almacen.Ruta(CarpetaCpe.Data, referencia, NombreArchivoCpe.Xml(referencia)) };
        await AlmacenCpe.EscribirAsync(rutas.Xml, generado.Xml!, ct).ConfigureAwait(false);
        if (!certificado.Exito)
        {
            return Resultado(referencia, fechaReferencia, [certificado.Error!], advertencias, rutas);
        }

        var firmado = firmador.Firmar(generado.Xml!, certificado.Certificado!, emisor.Certificado.Archivo);
        if (!firmado.Exito)
        {
            return Resultado(referencia, fechaReferencia, [firmado.Error!], advertencias, rutas);
        }

        var ahora = reloj.AhoraLima();
        rutas = rutas with
        {
            XmlFirmado = almacen.Ruta(CarpetaCpe.Firma, referencia, NombreArchivoCpe.Xml(referencia)),
            Zip = almacen.Ruta(CarpetaCpe.Envio, referencia, NombreArchivoCpe.Zip(referencia)),
            Historial = historial.Ruta(referencia),
        };
        await AlmacenCpe.EscribirAsync(rutas.XmlFirmado, firmado.Xml!, ct).ConfigureAwait(false);
        await AlmacenCpe.EscribirAsync(rutas.Zip, EmpaquetadorZip.Empaquetar(NombreArchivoCpe.Xml(referencia), firmado.Xml, ahora), ct).ConfigureAwait(false);

        var intento = new IntentoEnvio
        {
            Fecha = ahora,
            Operacion = ServicioCpe.OperacionGenerar,
            Canal = emisor.Canal.Clave,
            DuracionMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
            Estado = EstadoCpe.Generado,
        };
        await historial.RegistrarAsync(referencia, EstadoCpe.Generado, intento, documentos, ct).ConfigureAwait(false);
        RegistroEventos.DocumentoGenerado(registro, referencia.NombreArchivo, firmado.ValorResumen!);

        return Resultado(referencia, fechaReferencia, [], advertencias, rutas) with { ValorResumen = firmado.ValorResumen };
    }

    private ErrorCpe Error(string codigo, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, null, null, argumentos);

    /// <summary>El plazo del RC y de la RA se cuenta desde la emisión de sus documentos (la fecha de referencia).</summary>
    private ResultadoGeneracion Resultado(ReferenciaCpe referencia, DateOnly fechaReferencia, IReadOnlyList<ErrorCpe> encontrados, IReadOnlyList<ObservacionCpe> advertencias, RutasCpe rutas)
    {
        Telemetria.RegistrarErroresValidacion(referencia.TipoDocumento, encontrados);
        return new()
        {
            Referencia = referencia,
            Errores = encontrados,
            Advertencias = advertencias,
            Rutas = rutas,
            FechaLimiteEnvio = plazos.FechaLimite(referencia.TipoDocumento, null, fechaReferencia),
            FechaProceso = reloj.AhoraLima(),
        };
    }

    private static string Texto(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
