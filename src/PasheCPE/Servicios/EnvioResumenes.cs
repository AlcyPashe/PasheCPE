using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;
using PasheCPE.Respuestas;
using PasheCPE.Tiempo;
using PasheCPE.Transporte;
using PasheCPE.Transporte.Soap;
using PasheCPE.Validacion;

namespace PasheCPE.Servicios;

/// <summary>
/// Envío del resumen diario (RC) y de la comunicación de baja (RA) por <c>sendSummary</c> y consulta de su ticket por
/// <c>getStatus</c> (TRD §14 y §16).
/// <c>EnviarAsync</c> parte del ZIP de ENVIO y nunca regenera; antes de enviar lee el historial:
/// <list type="bullet">
/// <item>resultado definitivo (aceptado, observado, rechazado) → se devuelve sin llamar a SUNAT;</item>
/// <item>ya hay ticket → se consulta el ticket, no se reenvía (salvo que SUNAT responda que no existe, 0127);</item>
/// <item>sin ticket (respuesta incierta, servicio no disponible) → se reenvía el mismo ZIP.</item>
/// </list>
/// <c>sendSummary</c> solo se reintenta ante faults de disponibilidad; <c>getStatus</c>, hasta 3 veces. El resultado del
/// ticket se registra también en el historial de cada documento informado: en el RC, en proceso al recibir el ticket,
/// aceptado al recibir el CDR y, si el resumen se rechaza, las boletas adicionadas vuelven a <c>Generado</c> para otro
/// resumen; en la RA, los documentos pasan a <see cref="EstadoCpe.DeBaja"/> cuando SUNAT la acepta.
/// </summary>
internal sealed class EnvioResumenes(
    ClienteSoap cliente,
    LectorRespuestaSoap lectorSoap,
    LectorCdr lectorCdr,
    ClasificadorRespuesta clasificador,
    SoporteEnvio soporte,
    CatalogoErrores errores,
    CatalogoRespuestas respuestas,
    RegistroHistorial historial,
    BloqueoDocumentos bloqueos,
    TablaPlazos plazos,
    RelojCpe reloj,
    EsperasReintento esperas,
    TimeProvider tiempo,
    IOptions<OpcionesPasheCpe> opciones,
    ILogger<EnvioResumenes> registro)
{
    private const string _ticketInexistente = "0127";

    private static readonly TimeSpan[] _esperasTicketPorDefecto = [.. new[] { 2, 4, 8, 15, 30 }.Select(s => TimeSpan.FromSeconds(s))];

    public async Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(referencia);
        var contexto = new ContextoEnvio(referencia, OperacionSoap.SendSummary, soporte.Rutas(referencia));
        if (!File.Exists(contexto.Rutas.Zip))
        {
            return soporte.Local(contexto, "PCPE-4001", NombreArchivoCpe.Zip(referencia));
        }

        var (servicio, errorConfiguracion) = soporte.Preparar(emisor, contexto, ServicioSunat.Comprobantes);
        if (errorConfiguracion is not null)
        {
            return errorConfiguracion;
        }

        using var bloqueo = await bloqueos.AdquirirAsync(referencia.NombreArchivo, ct).ConfigureAwait(false);
        var previo = await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false);
        if (EsDefinitivo(previo?.EstadoActual))
        {
            return soporte.Completar(clasificador.DesdeHistorial(contexto, previo!), referencia);
        }

        if (await soporte.ConectividadAsync(emisor, contexto, servicio!.Url, ct).ConfigureAwait(false) is { } sinConexion)
        {
            return sinConexion;
        }

        // SUNAT ya recibió el resumen: se consulta el ticket en vez de reenviarlo.
        if (previo?.Ticket is { } ticketPrevio)
        {
            var consulta = await ConsultarConReintentosAsync(emisor, contexto with { Operacion = OperacionSoap.GetStatus }, servicio, ticketPrevio, previo, ct).ConfigureAwait(false);
            if (consulta.Codigo != _ticketInexistente)
            {
                return consulta;
            }
        }

        if (AnulacionFueraDePlazo(referencia, previo) is { } vencida)
        {
            return vencida with { Rutas = contexto.Rutas };
        }

        return await SendSummaryAsync(emisor, contexto, servicio, previo, ct).ConfigureAwait(false);
    }

    /// <summary>Un getStatus (con reintentos) del ticket indicado o, si es null, del último ticket del historial.</summary>
    public async Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(referencia);
        var contexto = new ContextoEnvio(referencia, OperacionSoap.GetStatus, soporte.Rutas(referencia));
        if (!EsResumenOBaja(referencia))
        {
            return soporte.Local(contexto, "PCPE-4015", referencia.NombreArchivo);
        }

        var (servicio, errorConfiguracion) = soporte.Preparar(emisor, contexto, ServicioSunat.Comprobantes);
        if (errorConfiguracion is not null)
        {
            return errorConfiguracion;
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

        if (await soporte.ConectividadAsync(emisor, contexto, servicio!.Url, ct).ConfigureAwait(false) is { } sinConexion)
        {
            return sinConexion with { Ticket = ticket };
        }

        return await ConsultarConReintentosAsync(emisor, contexto, servicio, ticket, previo, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Consulta el ticket con la espera progresiva de <see cref="OpcionesTickets.EsperasSegundos"/> (por defecto 2, 4, 8,
    /// 15 y 30 s): espera, consulta y repite mientras siga en proceso. Si se agotan las esperas devuelve
    /// <see cref="EstadoCpe.EnProceso"/> con el ticket, que queda en el historial para consultarlo después.
    /// </summary>
    public async Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket, CancellationToken ct)
    {
        ResultadoEnvio? resultado = null;
        foreach (var espera in EsperasTicket(opciones.Value))
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

    /// <summary>Esperas entre consultas del ticket: las de <see cref="OpcionesTickets.EsperasSegundos"/> o 2, 4, 8, 15 y 30 s (también las usa la GRE).</summary>
    public static IReadOnlyList<TimeSpan> EsperasTicket(OpcionesPasheCpe opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        var configuradas = opciones.Tickets.EsperasSegundos;
        return configuradas.Count > 0 ? [.. configuradas.Select(s => TimeSpan.FromSeconds(s))] : _esperasTicketPorDefecto;
    }

    /// <summary>sendSummary, reintentado solo ante faults de disponibilidad (SUNAT no recibió el archivo).</summary>
    private async Task<ResultadoEnvio> SendSummaryAsync(EmisorCpe emisor, ContextoEnvio contexto, EndpointCpe servicio, HistorialCpe? previo, CancellationToken ct)
    {
        var referencia = contexto.Referencia;
        var zip = await File.ReadAllBytesAsync(contexto.Rutas.Zip!, ct).ConfigureAwait(false);
        var credenciales = emisor.CredencialesSoap!;
        var sobre = ConstructorSobreSoap.SendSummary(ConstructorSobreSoap.UsuarioSoap(emisor, credenciales), credenciales.Clave, NombreArchivoCpe.Zip(referencia), Convert.ToBase64String(zip));
        for (var intento = 0; ; intento++)
        {
            using var actividad = Telemetria.Actividades.StartActivity(OperacionSoap.SendSummary);
            actividad?.SetTag("pashecpe.documento", referencia.NombreArchivo);
            var soap = await cliente.LlamarAsync(servicio.Url, OperacionSoap.SendSummary, servicio.SoapAction, sobre, ct).ConfigureAwait(false);
            var contenido = lectorSoap.Leer(soap.Cuerpo);
            ResultadoEnvio resultado;
            if (contenido?.Ticket is { } ticket)
            {
                resultado = clasificador.DesdeTicket(contexto, soap, ticket);
            }
            else if (contenido?.Fault is { } fault)
            {
                resultado = clasificador.DesdeFault(contexto, soap, fault);
            }
            else
            {
                resultado = clasificador.DesdeRed(contexto, soap, idempotente: false);
                soporte.InvalidarConectividad(servicio.Url, soap);
            }

            actividad?.SetTag("pashecpe.estado", resultado.Estado.ToString());
            resultado = await soporte.RegistrarAsync(emisor, referencia, resultado, resultado.Estado, ct).ConfigureAwait(false);
            if (resultado.Estado == EstadoCpe.EnProceso)
            {
                await PropagarAsync(emisor, referencia, previo?.Documentos, resultado, ct).ConfigureAwait(false);
                return resultado;
            }

            var disponibilidad = resultado.Estado == EstadoCpe.ServicioNoDisponible && errores.Buscar(resultado.Codigo)?.GrupoTransitorio == "disponibilidad";
            if (!disponibilidad || intento >= esperas.Esperas.Count)
            {
                return resultado;
            }

            RegistroEventos.Reintento(registro, intento + 1, OperacionSoap.SendSummary, referencia.NombreArchivo, resultado.Estado, resultado.Codigo);
            await Task.Delay(esperas.Esperas[intento], tiempo, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// getStatus con hasta 3 reintentos ante fallas de red o faults transitorios. El estado del resumen solo cambia con
    /// una respuesta concluyente (en proceso, aceptado, observado, rechazado); con un estado final se propaga a los
    /// documentos informados.
    /// </summary>
    private async Task<ResultadoEnvio> ConsultarConReintentosAsync(EmisorCpe emisor, ContextoEnvio contexto, EndpointCpe servicio, string ticket, HistorialCpe? previo, CancellationToken ct)
    {
        var referencia = contexto.Referencia;
        var credenciales = emisor.CredencialesSoap!;
        var sobre = ConstructorSobreSoap.GetStatus(ConstructorSobreSoap.UsuarioSoap(emisor, credenciales), credenciales.Clave, ticket);
        var estadoSinCambio = previo?.EstadoActual ?? EstadoCpe.EnProceso;
        for (var intento = 0; ; intento++)
        {
            using var actividad = Telemetria.Actividades.StartActivity(OperacionSoap.GetStatus);
            actividad?.SetTag("pashecpe.documento", referencia.NombreArchivo);
            var soap = await cliente.LlamarAsync(servicio.Url, OperacionSoap.GetStatus, servicio.SoapAction, sobre, ct).ConfigureAwait(false);
            var resultado = await InterpretarEstadoAsync(contexto, soap, ticket, ct).ConfigureAwait(false);
            var concluyente = resultado.Estado is EstadoCpe.EnProceso || EsDefinitivo(resultado.Estado);
            resultado = await soporte.RegistrarAsync(emisor, referencia, resultado with { Ticket = ticket }, concluyente ? resultado.Estado : estadoSinCambio, ct).ConfigureAwait(false);
            if (EsDefinitivo(resultado.Estado))
            {
                await PropagarAsync(emisor, referencia, previo?.Documentos, resultado, ct).ConfigureAwait(false);
            }

            if (resultado.Estado != EstadoCpe.ServicioNoDisponible || intento >= esperas.Esperas.Count)
            {
                return resultado;
            }

            RegistroEventos.Reintento(registro, intento + 1, OperacionSoap.GetStatus, referencia.NombreArchivo, resultado.Estado, resultado.Codigo);
            await Task.Delay(esperas.Esperas[intento], tiempo, ct).ConfigureAwait(false);
        }
    }

    private async Task<ResultadoEnvio> InterpretarEstadoAsync(ContextoEnvio contexto, RespuestaSoap soap, string ticket, CancellationToken ct)
    {
        var contenido = lectorSoap.Leer(soap.Cuerpo);
        if (contenido?.Fault is { } fault)
        {
            return clasificador.DesdeFault(contexto, soap, fault);
        }

        if (contenido?.Status is not { } estado)
        {
            soporte.InvalidarConectividad(soap.Endpoint, soap);
            return clasificador.DesdeRed(contexto, soap, idempotente: true);
        }

        if (estado.ContenidoBase64 is { } cdrBase64 && SoporteEnvio.TryBase64(cdrBase64) is { } cdrZip)
        {
            var (cdr, ruta) = await lectorCdr.GuardarYLeerAsync(contexto.Referencia, cdrZip, ct).ConfigureAwait(false);
            if (cdr is not null)
            {
                return clasificador.DesdeCdr(contexto with { Rutas = contexto.Rutas with { Cdr = ruta } }, soap, cdr) with { Ticket = ticket };
            }
        }

        return clasificador.DesdeEstadoTicket(contexto, soap, respuestas.EstadoTicket(estado.Codigo), estado.Codigo, ticket);
    }

    /// <summary>
    /// Registra el resultado del resumen en el historial de cada documento informado (decisión del 23/10/2026). Las
    /// boletas adicionadas pasan a en proceso, aceptadas u observadas y, si el resumen se rechaza, vuelven a
    /// <c>Generado</c>; las modificadas o anuladas conservan su estado (ya estaban aceptadas) y solo suman el intento.
    /// </summary>
    private async Task PropagarAsync(EmisorCpe emisor, ReferenciaCpe resumen, IReadOnlyList<DocumentoInformadoCpe>? documentos, ResultadoEnvio resultado, CancellationToken ct)
    {
        documentos ??= (await historial.ObtenerAsync(resumen, ct).ConfigureAwait(false))?.Documentos;
        if (documentos is null)
        {
            return;
        }

        var intento = SoporteEnvio.Intento(emisor, resultado) with { Descripcion = $"{resumen.NombreArchivo}: {resultado.Descripcion}" };
        if (resumen.TipoDocumento == ReferenciaCpe.TipoComunicacionBaja)
        {
            await PropagarBajaAsync(resumen, documentos, resultado, intento).ConfigureAwait(false);
            return;
        }

        foreach (var documento in documentos)
        {
            EstadoCpe estado;
            if (documento.Estado == EstadoItemResumen.Adicionar)
            {
                estado = resultado.Estado == EstadoCpe.Rechazado ? EstadoCpe.Generado : resultado.Estado;
            }
            else if (await historial.ObtenerAsync(documento.Referencia, ct).ConfigureAwait(false) is { } actual)
            {
                estado = actual.EstadoActual;
            }
            else
            {
                continue;
            }

            await historial.RegistrarAsync(documento.Referencia, estado, intento, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Baja (decisión del 27/10/2026): con la RA aceptada u observada los documentos pasan a <see cref="EstadoCpe.DeBaja"/>;
    /// rechazada, conservan su estado y dejan de estar en la RA (se pueden informar en otra); en proceso o con otro
    /// resultado, conservan su estado y suman el intento.
    /// </summary>
    private async Task PropagarBajaAsync(ReferenciaCpe baja, IReadOnlyList<DocumentoInformadoCpe> documentos, ResultadoEnvio resultado, IntentoEnvio intento)
    {
        var aceptada = resultado.Estado is EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones;
        EstadoCpe? estado = aceptada ? EstadoCpe.DeBaja : null;
        var vigente = resultado.Estado == EstadoCpe.Rechazado ? null : baja;
        foreach (var documento in documentos)
        {
            await historial.RegistrarBajaAsync(documento.Referencia, estado, aceptada ? intento with { Estado = EstadoCpe.DeBaja } : intento, vigente, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Una anulación fuera de plazo la rechaza SUNAT (2957): no se envía. En el RC son los documentos con estado 3; en la
    /// RA, todos, salvo las series numéricas, que la regla 2957 no alcanza. El plazo se vuelve a revisar al enviar porque
    /// puede vencer después de generar.
    /// </summary>
    private ResultadoEnvio? AnulacionFueraDePlazo(ReferenciaCpe referencia, HistorialCpe? previo)
    {
        var hoy = reloj.HoyLima();
        var esBaja = referencia.TipoDocumento == ReferenciaCpe.TipoComunicacionBaja;
        var vencida = previo?.Documentos?.FirstOrDefault(d => d.Estado == EstadoItemResumen.Anulado
            && !(esBaja && ValidadorBaja.EsSerieNumerica(d.Referencia.Serie))
            && plazos.FechaLimite(referencia.TipoDocumento, null, d.Referencia.Fecha) is { } limite && limite < hoy);
        if (vencida is null)
        {
            return null;
        }

        var limite = plazos.FechaLimite(referencia.TipoDocumento, null, vencida.Referencia.Fecha)!.Value;
        var contexto = new ContextoEnvio(previo!.Referencia, OperacionSoap.SendSummary, RutasCpe.Vacio);
        return soporte.Local(contexto, esBaja ? "PCPE-1510" : "PCPE-1313", vencida.Referencia.NombreArchivo, limite.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static bool EsResumenOBaja(ReferenciaCpe referencia) =>
        referencia.TipoDocumento is ReferenciaCpe.TipoResumenDiario or ReferenciaCpe.TipoComunicacionBaja;

    private static bool EsDefinitivo(EstadoCpe? estado) =>
        estado is EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones or EstadoCpe.Rechazado;
}
