using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;
using PasheCPE.Qr;
using PasheCPE.Respuestas;
using PasheCPE.Tiempo;
using PasheCPE.Transporte;
using PasheCPE.Transporte.Gre;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Servicios;

/// <summary>
/// Envío de la guía de remisión por la API REST de SUNAT (TRD §17) y consulta de su ticket. La GRE solo existe en
/// producción: siempre usa los endpoints de SUNAT en producción, aunque los comprobantes del emisor vayan por OSE, y con
/// <c>Ambiente = Beta</c> no se envía salvo que <see cref="OpcionesGre.PermitirEnvioConAmbienteBeta"/>. Como el RC,
/// <c>EnviarAsync</c> parte del ZIP de ENVIO y nunca regenera:
/// <list type="bullet">
/// <item>resultado definitivo (aceptado, observado, rechazado) → se devuelve sin llamar a SUNAT;</item>
/// <item>ya hay ticket → se consulta el ticket, no se reenvía;</item>
/// <item>sin ticket (respuesta incierta, servicio no disponible) → se reenvía el mismo ZIP: la API no tiene una
/// consulta por número.</item>
/// </list>
/// Un 401 descarta el token, pide otro y repite la llamada una vez. La consulta del ticket se reintenta hasta 3 veces;
/// el envío, nunca. Al aceptarse la guía se guarda el CDR y se genera el QR con el enlace que trae. Nunca lanza.
/// </summary>
internal sealed class EnvioGre(
    ClienteGre cliente,
    ProveedorTokenGre tokens,
    LectorCdr lectorCdr,
    ClasificadorRespuesta clasificador,
    SoporteEnvio soporte,
    CatalogoEndpoints endpoints,
    CatalogoErrores errores,
    CatalogoRespuestas respuestas,
    RegistroHistorial historial,
    BloqueoDocumentos bloqueos,
    TablaPlazos plazos,
    RelojCpe reloj,
    AlmacenCpe almacen,
    EsperasReintento esperas,
    TimeProvider tiempo,
    IOptions<OpcionesPasheCpe> opciones,
    ILogger<EnvioGre> registro)
{
    private const int _noAutorizado = 401;

    public async Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(referencia);
        emisor = emisor with { Canal = CanalEnvio.Sunat };
        var contexto = new ContextoEnvio(referencia, OperacionGre.Envio, soporte.Rutas(referencia));
        var (token, api, error) = Preparar(emisor, contexto);
        if (error is not null)
        {
            return error;
        }

        if (!File.Exists(contexto.Rutas.Zip))
        {
            return soporte.Local(contexto, "PCPE-4001", NombreArchivoCpe.Zip(referencia));
        }

        using var bloqueo = await bloqueos.AdquirirAsync(referencia.NombreArchivo, ct).ConfigureAwait(false);
        var previo = await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false);
        if (EsDefinitivo(previo?.EstadoActual))
        {
            return soporte.Completar(clasificador.DesdeHistorial(contexto, previo!), referencia);
        }

        if (await soporte.ConectividadAsync(emisor, contexto, api!.Url, ct).ConfigureAwait(false) is { } sinConexion)
        {
            return sinConexion;
        }

        // SUNAT ya recibió la guía: se consulta el ticket en vez de reenviarla.
        if (previo?.Ticket is { } ticketPrevio)
        {
            return await ConsultarConReintentosAsync(emisor, contexto with { Operacion = OperacionGre.Consulta }, token!, api, ticketPrevio, previo, ct).ConfigureAwait(false);
        }

        if (plazos.FechaLimite(referencia) is { } limite && limite < reloj.HoyLima())
        {
            return soporte.Local(contexto, "PCPE-4006", referencia.NombreArchivo, limite.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
        }

        var zip = await File.ReadAllBytesAsync(contexto.Rutas.Zip!, ct).ConfigureAwait(false);
        using var actividad = Telemetria.Actividades.StartActivity(OperacionGre.Envio);
        actividad?.SetTag("pashecpe.documento", referencia.NombreArchivo);
        var (respuesta, fallaToken) = await ConTokenAsync(emisor, token!, (valor, c) => cliente.EnviarAsync(api.Url, valor, referencia.NombreArchivo, zip, c), ct).ConfigureAwait(false);
        ResultadoEnvio resultado;
        if (fallaToken is not null)
        {
            resultado = FallaToken(contexto, fallaToken);
        }
        else if (respuesta!.Falla != FallaRed.Ninguna || respuesta.Http is >= 500 or (>= 200 and < 300))
        {
            resultado = ClienteGre.LeerTicket(respuesta.Cuerpo) is { } ticket && respuesta.Http is >= 200 and < 300
                ? clasificador.DesdeTicket(contexto, respuesta, ticket.NumTicket)
                : clasificador.DesdeRed(contexto, respuesta, idempotente: false);
            soporte.InvalidarConectividad(api.Url, respuesta);
        }
        else
        {
            resultado = clasificador.DesdeErrorGre(contexto, respuesta, ClienteGre.LeerError(respuesta.Cuerpo));
        }

        actividad?.SetTag("pashecpe.estado", resultado.Estado.ToString());
        return await soporte.RegistrarAsync(emisor, referencia, resultado, resultado.Estado, ct).ConfigureAwait(false);
    }

    /// <summary>Una consulta (con reintentos) del ticket indicado o, si es null, del último ticket del historial.</summary>
    public async Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(referencia);
        emisor = emisor with { Canal = CanalEnvio.Sunat };
        var contexto = new ContextoEnvio(referencia, OperacionGre.Consulta, soporte.Rutas(referencia));
        var (token, api, error) = Preparar(emisor, contexto);
        if (error is not null)
        {
            return error;
        }

        using var bloqueo = await bloqueos.AdquirirAsync(referencia.NombreArchivo, ct).ConfigureAwait(false);
        var previo = await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false);
        ticket ??= previo?.Ticket;
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return soporte.Local(contexto, "PCPE-4011", referencia.NombreArchivo);
        }

        if (EsDefinitivo(previo?.EstadoActual) && ticket == previo!.Ticket)
        {
            return soporte.Completar(clasificador.DesdeHistorial(contexto, previo) with { Ticket = ticket }, referencia);
        }

        if (await soporte.ConectividadAsync(emisor, contexto, api!.Url, ct).ConfigureAwait(false) is { } sinConexion)
        {
            return sinConexion with { Ticket = ticket };
        }

        return await ConsultarConReintentosAsync(emisor, contexto, token!, api, ticket, previo, ct).ConfigureAwait(false);
    }

    /// <summary>Consulta el ticket con la espera progresiva del resumen diario hasta que deje de estar en proceso.</summary>
    public async Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket, CancellationToken ct)
    {
        ResultadoEnvio? resultado = null;
        foreach (var espera in EnvioResumenes.EsperasTicket(opciones.Value))
        {
            await Task.Delay(espera, tiempo, ct).ConfigureAwait(false);
            resultado = await ConsultarTicketAsync(emisor, referencia, ticket, ct).ConfigureAwait(false);
            if (resultado.Estado != EstadoCpe.EnProceso)
            {
                return resultado;
            }
        }

        return resultado ?? await ConsultarTicketAsync(emisor, referencia, ticket, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tipo de documento, credenciales de la API, protección de beta y endpoints de producción (token y API); un error de
    /// configuración no llama a SUNAT.
    /// </summary>
    private (EndpointCpe? Token, EndpointCpe? Api, ResultadoEnvio? Error) Preparar(EmisorCpe emisor, ContextoEnvio contexto)
    {
        var referencia = contexto.Referencia;
        if (referencia.TipoDocumento != TipoDocumentoSunat.GuiaDeRemisionRemitente)
        {
            return (null, null, soporte.Local(contexto, "PCPE-4604", referencia.NombreArchivo));
        }

        if (emisor.CredencialesGre is not { ClientId.Length: > 0, ClientSecret.Length: > 0, UsuarioSol.Length: > 0, ClaveSol.Length: > 0 })
        {
            return (null, null, soporte.Local(contexto, "PCPE-4601"));
        }

        if (opciones.Value.Ambiente != Ambiente.Produccion && !opciones.Value.Gre.PermitirEnvioConAmbienteBeta)
        {
            return (null, null, soporte.Local(contexto, "PCPE-4602", referencia.NombreArchivo));
        }

        var token = endpoints.Obtener(CanalEnvio.ClaveSunat, Ambiente.Produccion, ServicioSunat.GreToken);
        var api = endpoints.Obtener(CanalEnvio.ClaveSunat, Ambiente.Produccion, ServicioSunat.GreApi);
        return token is null || api is null
            ? (null, null, soporte.Local(contexto, "PCPE-4004", token is null ? nameof(ServicioSunat.GreToken) : nameof(ServicioSunat.GreApi), CanalEnvio.ClaveSunat, Ambiente.Produccion))
            : (token, api, null);
    }

    /// <summary>
    /// Ejecuta la llamada con el token vigente; con 401 descarta ese token, pide otro y la repite una sola vez. Devuelve la
    /// respuesta o, si no se pudo obtener el token, la respuesta del servicio de tokens.
    /// </summary>
    private async Task<(RespuestaSoap? Respuesta, RespuestaSoap? FallaToken)> ConTokenAsync(EmisorCpe emisor, EndpointCpe urlToken, Func<string, CancellationToken, Task<RespuestaSoap>> llamada, CancellationToken ct)
    {
        string? rechazado = null;
        for (var intento = 0; ; intento++)
        {
            var token = await tokens.ObtenerAsync(urlToken, emisor.Ruc, emisor.CredencialesGre!, rechazado, ct).ConfigureAwait(false);
            if (token.Token is null)
            {
                return (null, token.Falla);
            }

            var respuesta = await llamada(token.Token, ct).ConfigureAwait(false);
            if (respuesta.Http != _noAutorizado || intento > 0)
            {
                return (respuesta, null);
            }

            rechazado = token.Token;
        }
    }

    /// <summary>
    /// Consulta del ticket con hasta 3 reintentos ante fallas de red o HTTP 5xx. El estado de la guía solo cambia con
    /// una respuesta concluyente (en proceso, aceptada, observada, rechazada).
    /// </summary>
    private async Task<ResultadoEnvio> ConsultarConReintentosAsync(EmisorCpe emisor, ContextoEnvio contexto, EndpointCpe urlToken, EndpointCpe api, string ticket, HistorialCpe? previo, CancellationToken ct)
    {
        var referencia = contexto.Referencia;
        var estadoSinCambio = previo?.EstadoActual ?? EstadoCpe.EnProceso;
        for (var intento = 0; ; intento++)
        {
            using var actividad = Telemetria.Actividades.StartActivity(OperacionGre.Consulta);
            actividad?.SetTag("pashecpe.documento", referencia.NombreArchivo);
            var (respuesta, fallaToken) = await ConTokenAsync(emisor, urlToken, (valor, c) => cliente.ConsultarAsync(api.Url, valor, ticket, c), ct).ConfigureAwait(false);
            var resultado = fallaToken is not null
                ? FallaToken(contexto, fallaToken)
                : await InterpretarEstadoAsync(contexto, respuesta!, ticket, ct).ConfigureAwait(false);
            var concluyente = resultado.Estado is EstadoCpe.EnProceso || EsDefinitivo(resultado.Estado);
            resultado = await soporte.RegistrarAsync(emisor, referencia, resultado with { Ticket = ticket }, concluyente ? resultado.Estado : estadoSinCambio, ct).ConfigureAwait(false);
            if (resultado.Estado != EstadoCpe.ServicioNoDisponible || intento >= esperas.Esperas.Count)
            {
                return resultado;
            }

            RegistroEventos.Reintento(registro, intento + 1, OperacionGre.Consulta, referencia.NombreArchivo, resultado.Estado, resultado.Codigo);
            await Task.Delay(esperas.Esperas[intento], tiempo, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// codRespuesta 0 o 99 con CDR → se clasifica el CDR (al aceptarse, QR con su enlace); 98 → en proceso; 99 sin CDR →
    /// el error del ticket; HTTP 4xx → error de la API; sin respuesta legible → servicio no disponible (se reintenta).
    /// </summary>
    private async Task<ResultadoEnvio> InterpretarEstadoAsync(ContextoEnvio contexto, RespuestaSoap respuesta, string ticket, CancellationToken ct)
    {
        if (respuesta.Falla == FallaRed.Ninguna && respuesta.Http is >= 400 and < 500)
        {
            return clasificador.DesdeErrorGre(contexto, respuesta, ClienteGre.LeerError(respuesta.Cuerpo)) with { Ticket = ticket };
        }

        if (respuesta.Http is not (>= 200 and < 300) || ClienteGre.LeerEstado(respuesta.Cuerpo) is not { } estado)
        {
            soporte.InvalidarConectividad(respuesta.Endpoint, respuesta);
            return clasificador.DesdeRed(contexto, respuesta, idempotente: true);
        }

        if (estado.ArcCdr is { } cdrBase64 && SoporteEnvio.TryBase64(cdrBase64) is { } cdrZip)
        {
            var (cdr, ruta) = await lectorCdr.GuardarYLeerAsync(contexto.Referencia, cdrZip, ct).ConfigureAwait(false);
            if (cdr is not null)
            {
                var resultado = clasificador.DesdeCdr(contexto with { Rutas = contexto.Rutas with { Cdr = ruta } }, respuesta, cdr) with { Ticket = ticket };
                return resultado.Estado is EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones && cdr.EnlaceQr is { } enlace
                    ? await ConQrAsync(contexto.Referencia, resultado, enlace, ct).ConfigureAwait(false)
                    : resultado;
            }
        }

        var estadoTicket = respuestas.EstadoTicket(estado.CodRespuesta);
        return estadoTicket?.Resultado == ResultadoTicket.ConErrores && (estado.NumError is not null || estado.DesError is not null)
            ? clasificador.DesdeErrorTicketGre(contexto, respuesta, estado.NumError, estado.DesError, ticket)
            : clasificador.DesdeEstadoTicket(contexto, respuesta, estadoTicket, estado.CodRespuesta, ticket);
    }

    /// <summary>QR de la guía (TRD §12): el enlace que SUNAT devuelve en el CDR, como PNG en QR\yyyy\MM.</summary>
    private async Task<ResultadoEnvio> ConQrAsync(ReferenciaCpe referencia, ResultadoEnvio resultado, string enlace, CancellationToken ct)
    {
        var ruta = almacen.Ruta(CarpetaCpe.Qr, referencia, NombreArchivoCpe.Qr(referencia));
        await AlmacenCpe.EscribirAsync(ruta, GeneradorQrPng.Generar(enlace), ct).ConfigureAwait(false);
        return resultado with { ContenidoQr = enlace, Rutas = resultado.Rutas with { Qr = ruta } };
    }

    /// <summary>El token no se obtuvo: sin red o 5xx → servicio no disponible; credenciales rechazadas → error de configuración.</summary>
    private ResultadoEnvio FallaToken(ContextoEnvio contexto, RespuestaSoap respuesta)
    {
        var tokenContexto = contexto with { Operacion = OperacionGre.Token };
        if (respuesta.Falla != FallaRed.Ninguna || respuesta.Http is >= 500)
        {
            return clasificador.DesdeRed(tokenContexto, respuesta, idempotente: true);
        }

        var detalle = ClienteGre.LeerError(respuesta.Cuerpo)?.Msg ?? respuesta.Cuerpo ?? string.Empty;
        return clasificador.Local(tokenContexto, EstadoCpe.ErrorServicio, errores.Local("PCPE-4603", OrigenError.Sunat, null, null, respuesta.Http, detalle));
    }

    private static bool EsDefinitivo(EstadoCpe? estado) =>
        estado is EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones or EstadoCpe.Rechazado;
}
