using System.Diagnostics;
using Microsoft.Extensions.Logging;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;
using PasheCPE.Firma;
using PasheCPE.Mapeo;
using PasheCPE.Tiempo;

namespace PasheCPE.Servicios;

/// <summary>
/// Fachada de guías (<see cref="IServicioGre"/>). Pipeline de generación (TRD §6): historial → XML validado en DATA
/// (<see cref="GeneradorGuia"/>) → certificado → firma (FIRMA) → ZIP (ENVIO) → historial (LOG); a diferencia de los
/// comprobantes, el QR se genera al aceptarse (<see cref="EnvioGre"/>). El envío y el ticket los hace
/// <see cref="EnvioGre"/>.
/// </summary>
internal sealed class ServicioGre(
    GeneradorGuia generador,
    EnvioGre envio,
    ProveedorCertificados certificados,
    FirmadorXml firmador,
    AlmacenCpe almacen,
    RegistroHistorial historial,
    TablaPlazos plazos,
    CatalogoErrores errores,
    CatalogoRespuestas respuestas,
    RelojCpe reloj,
    ILogger<ServicioGre> registro) : IServicioGre
{
    public async Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(guia);
        var inicio = Stopwatch.GetTimestamp();
        var referencia = generador.Referencia(emisor, guia);

        // Regenerar lo ya enviado dejaría archivos distintos de los que tiene SUNAT.
        if (await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false) is { EstadoActual: not (EstadoCpe.Generado or EstadoCpe.ErrorValidacionLocal) } previo)
        {
            return Resultado(referencia, [errores.Local("PCPE-3101", OrigenError.Modelo, null, null, referencia.NombreArchivo, previo.EstadoActual)], [], RutasCpe.Vacio);
        }

        var generado = await generador.GenerarAsync(emisor, guia, ct).ConfigureAwait(false);
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
            Canal = CanalEnvio.ClaveSunat,
            DuracionMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
            Estado = EstadoCpe.Generado,
        };
        await historial.RegistrarAsync(referencia, EstadoCpe.Generado, intento, ct).ConfigureAwait(false);
        RegistroEventos.DocumentoGenerado(registro, referencia.NombreArchivo, firmado.ValorResumen!);

        return Resultado(referencia, [], advertencias, rutas) with { ValorResumen = firmado.ValorResumen };
    }

    public Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var generado = generador.Construir(emisor, guia);
        return Task.FromResult(new ResultadoValidacion { Errores = generado.Errores, Advertencias = generado.Advertencias });
    }

    public Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default) =>
        ServicioCpe.ConReceptorAsync(CanalEnvio.Sunat, envio.EnviarAsync(emisor, referencia, ct));

    public Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default) =>
        ServicioCpe.ConReceptorAsync(CanalEnvio.Sunat, envio.ConsultarTicketAsync(emisor, referencia, ticket, ct));

    public Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default) =>
        ServicioCpe.ConReceptorAsync(CanalEnvio.Sunat, envio.EsperarTicketAsync(emisor, referencia, ticket, ct));

    public async Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default)
    {
        var generacion = await GenerarAsync(emisor, guia, ct).ConfigureAwait(false);
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
                    MensajeUsuario = CatalogoRespuestas.ConReceptor(respuestas.MensajeUsuario(EstadoCpe.ErrorValidacionLocal, null), CanalEnvio.Sunat),
                    Errores = generacion.Errores,
                    Rutas = generacion.Rutas,
                    FechaLimiteEnvio = generacion.FechaLimiteEnvio,
                    FechaProceso = generacion.FechaProceso,
                },
            };
        }

        var enviado = await EnviarAsync(emisor, generacion.Referencia, ct).ConfigureAwait(false);
        if (enviado.Estado == EstadoCpe.EnProceso && enviado.Ticket is { } ticket)
        {
            enviado = await EsperarTicketAsync(emisor, generacion.Referencia, ticket, ct).ConfigureAwait(false);
        }

        return new ResultadoEmision { Generacion = generacion, Envio = enviado with { ValorResumen = generacion.ValorResumen } };
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
