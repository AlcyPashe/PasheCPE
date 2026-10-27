using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;
using PasheCPE.Firma;
using PasheCPE.Mapeo;
using PasheCPE.Qr;
using PasheCPE.Tiempo;

namespace PasheCPE.Servicios;

/// <summary>
/// Fachada de comprobantes (<see cref="IServicioCpe"/>). Pipeline de generación (TRD §6): historial → XML validado en
/// DATA (<see cref="GeneradorInvoice"/> o <see cref="GeneradorNota"/>) → certificado → firma (FIRMA) → ZIP (ENVIO) → QR
/// → historial (LOG). Cada
/// etapa persiste su archivo y el resultado devuelve todas las rutas; con un error se detiene sin escribir las
/// siguientes. El resumen diario y la baja los genera <see cref="GeneracionResumenes"/>; el envío y la consulta los
/// hacen <see cref="EnvioComprobantes"/> (sendBill, getStatusCdr) y <see cref="EnvioResumenes"/> (sendSummary, getStatus).
/// </summary>
internal sealed class ServicioCpe(
    GeneradorInvoice generador,
    GeneradorNota generadorNotas,
    GeneracionResumenes resumenes,
    EnvioComprobantes envio,
    EnvioResumenes envioResumenes,
    ProveedorCertificados certificados,
    FirmadorXml firmador,
    ConstructorContenidoQr qr,
    AlmacenCpe almacen,
    RegistroHistorial historial,
    TablaPlazos plazos,
    CatalogoErrores errores,
    CatalogoRespuestas respuestas,
    RelojCpe reloj,
    ILogger<ServicioCpe> registro) : IServicioCpe
{
    public const string OperacionGenerar = "generar";

    public Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(cpe);
        return GenerarAsync(emisor, generador.Referencia(emisor, cpe), () => generador.GenerarAsync(emisor, cpe, ct), ct);
    }

    public Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(nota);
        return GenerarAsync(emisor, generadorNotas.Referencia(emisor, nota), () => generadorNotas.GenerarAsync(emisor, nota, ct), ct);
    }

    private async Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, ReferenciaCpe referencia, Func<Task<XmlGenerado>> generar, CancellationToken ct)
    {
        var inicio = Stopwatch.GetTimestamp();

        // Regenerar lo ya enviado cambiaría el valor resumen y dejaría archivos distintos de los que tiene SUNAT.
        if (await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false) is { EstadoActual: not (EstadoCpe.Generado or EstadoCpe.ErrorValidacionLocal) } previo)
        {
            return Resultado(referencia, [errores.Local("PCPE-3101", OrigenError.Modelo, null, null, referencia.NombreArchivo, previo.EstadoActual)], [], RutasCpe.Vacio);
        }

        var generado = await generar().ConfigureAwait(false);
        var rutas = new RutasCpe { Xml = generado.Ruta };
        if (!generado.Exito)
        {
            return Resultado(referencia, generado.Errores, generado.Advertencias, rutas);
        }

        var certificado = certificados.Obtener(emisor.Certificado);
        IReadOnlyList<ObservacionCpe> advertencias = certificado.Advertencia is { } aviso ? [.. generado.Advertencias, aviso] : generado.Advertencias;
        if (!certificado.Exito)
        {
            return Resultado(referencia, [certificado.Error!], advertencias, rutas);
        }

        var firmado = firmador.Firmar(generado.Xml!, certificado.Certificado!, emisor.Certificado.Archivo);
        if (!firmado.Exito)
        {
            return Resultado(referencia, [firmado.Error!], advertencias, rutas);
        }

        var ahora = reloj.AhoraLima();
        var contenidoQr = qr.Construir(generado.Calculo!, firmado.ValorResumen!);
        rutas = rutas with
        {
            XmlFirmado = almacen.Ruta(CarpetaCpe.Firma, referencia, NombreArchivoCpe.Xml(referencia)),
            Zip = almacen.Ruta(CarpetaCpe.Envio, referencia, NombreArchivoCpe.Zip(referencia)),
            Qr = almacen.Ruta(CarpetaCpe.Qr, referencia, NombreArchivoCpe.Qr(referencia)),
            Historial = historial.Ruta(referencia),
        };
        await AlmacenCpe.EscribirAsync(rutas.XmlFirmado, firmado.Xml!, ct).ConfigureAwait(false);
        await AlmacenCpe.EscribirAsync(rutas.Zip, EmpaquetadorZip.Empaquetar(NombreArchivoCpe.Xml(referencia), firmado.Xml, ahora), ct).ConfigureAwait(false);
        await AlmacenCpe.EscribirAsync(rutas.Qr, GeneradorQrPng.Generar(contenidoQr), ct).ConfigureAwait(false);

        var intento = new IntentoEnvio
        {
            Fecha = ahora,
            Operacion = OperacionGenerar,
            Canal = emisor.Canal.Clave,
            DuracionMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
            Estado = EstadoCpe.Generado,
        };
        await historial.RegistrarAsync(referencia, EstadoCpe.Generado, intento, ct).ConfigureAwait(false);
        RegistroEventos.DocumentoGenerado(registro, referencia.NombreArchivo, firmado.ValorResumen!);

        return Resultado(referencia, [], advertencias, rutas) with
        {
            ValorResumen = firmado.ValorResumen,
            ContenidoQr = contenidoQr,
        };
    }

    public Task<IReadOnlyList<ResultadoGeneracion>> GenerarResumenAsync(EmisorCpe emisor, ResumenDiarioCpe resumen, CancellationToken ct = default) =>
        resumenes.GenerarAsync(emisor, resumen, ct);

    public Task<IReadOnlyList<ResultadoGeneracion>> GenerarResumenAsync(EmisorCpe emisor, DateOnly fechaReferencia, IReadOnlyList<DocumentoResumenCpe> documentos, CancellationToken ct = default) =>
        resumenes.GenerarAsync(emisor, fechaReferencia, documentos, ct);

    public Task<ResultadoGeneracion> GenerarBajaAsync(EmisorCpe emisor, ComunicacionBajaCpe baja, CancellationToken ct = default) =>
        resumenes.GenerarBajaAsync(emisor, baja, ct);

    public Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        return referencia.TipoDocumento is ReferenciaCpe.TipoResumenDiario or ReferenciaCpe.TipoComunicacionBaja
            ? envioResumenes.EnviarAsync(emisor, referencia, ct)
            : envio.EnviarAsync(emisor, referencia, ct);
    }

    public Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default) =>
        envioResumenes.ConsultarTicketAsync(emisor, referencia, ticket, ct);

    public Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default) =>
        envioResumenes.EsperarTicketAsync(emisor, referencia, ticket, ct);

    public Task<ResultadoEnvio> ConsultarCdrAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default) =>
        envio.ConsultarCdrAsync(emisor, referencia, ct);

    public async Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default) =>
        await EmitirAsync(emisor, await GenerarAsync(emisor, cpe, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    public async Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default) =>
        await EmitirAsync(emisor, await GenerarAsync(emisor, nota, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    private async Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, ResultadoGeneracion generacion, CancellationToken ct)
    {
        if (!generacion.Exito)
        {
            return new ResultadoEmision
            {
                Generacion = generacion,
                Envio = new ResultadoEnvio
                {
                    Estado = EstadoCpe.ErrorValidacionLocal,
                    Codigo = generacion.Errores[0].Codigo,
                    Descripcion = generacion.Errores[0].Descripcion,
                    MensajeUsuario = respuestas.MensajeUsuario(EstadoCpe.ErrorValidacionLocal, null),
                    Errores = generacion.Errores,
                    Rutas = generacion.Rutas,
                    FechaLimiteEnvio = generacion.FechaLimiteEnvio,
                    FechaProceso = generacion.FechaProceso,
                },
            };
        }

        var enviado = await envio.EnviarAsync(emisor, generacion.Referencia, ct).ConfigureAwait(false);
        return new ResultadoEmision
        {
            Generacion = generacion,
            Envio = enviado with { ValorResumen = generacion.ValorResumen, ContenidoQr = generacion.ContenidoQr },
        };
    }

    public Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var generado = generador.Construir(emisor, cpe);
        return Task.FromResult(new ResultadoValidacion { Errores = generado.Errores, Advertencias = generado.Advertencias });
    }

    public async Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var generado = await generadorNotas.ConstruirAsync(emisor, nota, ct).ConfigureAwait(false);
        return new ResultadoValidacion { Errores = generado.Errores, Advertencias = generado.Advertencias };
    }

    private ResultadoGeneracion Resultado(ReferenciaCpe referencia, IReadOnlyList<ErrorCpe> encontrados, IReadOnlyList<ObservacionCpe> advertencias, RutasCpe rutas) => new()
    {
        Referencia = referencia,
        Errores = encontrados,
        Advertencias = advertencias,
        Rutas = rutas,
        FechaLimiteEnvio = plazos.FechaLimite(referencia),
        FechaProceso = reloj.AhoraLima(),
    };
}
