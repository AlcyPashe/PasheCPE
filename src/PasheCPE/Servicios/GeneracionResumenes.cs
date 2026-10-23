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
/// Generación del resumen diario (TRD §6, PRD RF-18). Bajo el bloqueo del día (<see cref="AsignadorCorrelativos"/>):
/// valida el modelo y el historial de los documentos → asigna los correlativos → divide en lotes de 500 → por cada lote,
/// XML validado (DATA) → firma (FIRMA) → ZIP (ENVIO) → historial (LOG) en <c>Generado</c> con los documentos informados.
/// Un error del modelo detiene todo; un lote con errores no se escribe y no impide los demás. La vía «desde FIRMA»
/// arma las líneas leyendo el XML firmado de cada documento.
/// </summary>
internal sealed class GeneracionResumenes(
    GeneradorSummary generador,
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
        var (encontrados, advertencias) = await generador.ValidarAsync(emisor, resumen, fechaGeneracion, ct).ConfigureAwait(false);
        if (encontrados.Count > 0)
        {
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
            resultados.Add(await GenerarLoteAsync(emisor, lote, certificado, advertencias, inicio, ct).ConfigureAwait(false));
        }

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

    private async Task<ResultadoGeneracion> GenerarLoteAsync(EmisorCpe emisor, LoteResumen lote, CertificadoObtenido certificado, IReadOnlyList<ObservacionCpe> advertencias, long inicio, CancellationToken ct)
    {
        var referencia = lote.Referencia;
        var generado = generador.Construir(lote);
        advertencias = [.. advertencias, .. generado.Advertencias];
        if (!generado.Exito)
        {
            return Resultado(referencia, lote.FechaReferencia, generado.Errores, advertencias, RutasCpe.Vacio);
        }

        var rutas = new RutasCpe { Xml = almacen.Ruta(CarpetaCpe.Data, referencia, NombreArchivoCpe.Xml(referencia)) };
        await AlmacenCpe.EscribirAsync(rutas.Xml, generado.Xml!, ct).ConfigureAwait(false);
        if (!certificado.Exito)
        {
            return Resultado(referencia, lote.FechaReferencia, [certificado.Error!], advertencias, rutas);
        }

        var firmado = firmador.Firmar(generado.Xml!, certificado.Certificado!, emisor.Certificado.Archivo);
        if (!firmado.Exito)
        {
            return Resultado(referencia, lote.FechaReferencia, [firmado.Error!], advertencias, rutas);
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
        IReadOnlyList<DocumentoInformadoCpe> documentos = [.. lote.Lineas.Select(l => new DocumentoInformadoCpe
        {
            Referencia = ReferenciaCpe.Comprobante(emisor.Ruc, l.TipoDocumento, l.Serie, l.Numero, lote.FechaReferencia),
            Estado = l.Estado,
        })];
        await historial.RegistrarAsync(referencia, EstadoCpe.Generado, intento, documentos, ct).ConfigureAwait(false);
        RegistroEventos.DocumentoGenerado(registro, referencia.NombreArchivo, firmado.ValorResumen!);

        return Resultado(referencia, lote.FechaReferencia, [], advertencias, rutas) with { ValorResumen = firmado.ValorResumen };
    }

    private ErrorCpe Error(string codigo, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, null, null, argumentos);

    private ResultadoGeneracion Resultado(ReferenciaCpe referencia, DateOnly fechaReferencia, IReadOnlyList<ErrorCpe> encontrados, IReadOnlyList<ObservacionCpe> advertencias, RutasCpe rutas) => new()
    {
        Referencia = referencia,
        Errores = encontrados,
        Advertencias = advertencias,
        Rutas = rutas,
        FechaLimiteEnvio = plazos.FechaLimite(ReferenciaCpe.TipoResumenDiario, null, fechaReferencia),
        FechaProceso = reloj.AhoraLima(),
    };

    private static string Texto(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
