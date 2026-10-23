using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Conectividad;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;
using PasheCPE.Respuestas;
using PasheCPE.Tiempo;
using PasheCPE.Transporte;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Servicios;

/// <summary>
/// Envío de comprobantes por <c>sendBill</c> y consulta por <c>getStatusCdr</c> (TRD §14). <c>EnviarAsync</c> parte del
/// ZIP de ENVIO y nunca regenera; antes de reenviar lee el historial:
/// <list type="bullet">
/// <item>resultado definitivo (aceptado, observado, rechazado) → se devuelve sin llamar a SUNAT;</item>
/// <item>último envío incierto, en proceso, con fault de constancia o 1033 → primero <c>getStatusCdr</c>; si no se puede
/// consultar (beta, boletas o series B) se reenvía el mismo ZIP (decisión del 23/10/2026);</item>
/// <item>fault de disponibilidad o error de servicio → se reenvía directamente.</item>
/// </list>
/// <c>sendBill</c> nunca se reintenta dentro de la llamada; <c>getStatusCdr</c>, hasta 3 veces. Un bloqueo por documento
/// impide envíos simultáneos. Ninguna respuesta de negocio o de red lanza excepción.
/// </summary>
internal sealed class EnvioComprobantes(
    ClienteSoap cliente,
    LectorRespuestaSoap lectorSoap,
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
    EsperasReintento esperas,
    TimeProvider tiempo,
    IOptions<OpcionesPasheCpe> opciones,
    ILogger<EnvioComprobantes> registro)
{
    public async Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(referencia);
        var contexto = new ContextoEnvio(referencia, OperacionSoap.SendBill, soporte.Rutas(referencia));
        if (referencia.TipoDocumento is not (TipoDocumentoSunat.Factura or TipoDocumentoSunat.BoletaDeVenta or TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito))
        {
            return soporte.Local(contexto, "PCPE-4007", referencia.NombreArchivo);
        }

        if (EsBoleta(referencia) && opciones.Value.ModoEnvioBoletas == ModoEnvioBoletas.ResumenDiario)
        {
            return soporte.Local(contexto, "PCPE-4002", referencia.NombreArchivo);
        }

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
        if (previo?.EstadoActual is EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones or EstadoCpe.Rechazado)
        {
            return soporte.Completar(clasificador.DesdeHistorial(contexto, previo), referencia);
        }

        if (await soporte.ConectividadAsync(emisor, contexto, servicio!.Url, ct).ConfigureAwait(false) is { } sinConexion)
        {
            return sinConexion;
        }

        if (RequiereConsultaPrevia(previo))
        {
            if (PuedeConsultar(emisor, referencia, out var consulta))
            {
                var (resultadoConsulta, sigueSinExistir) = await ConsultarConReintentosAsync(emisor, contexto, consulta, previo?.EstadoActual, ct).ConfigureAwait(false);
                if (!sigueSinExistir)
                {
                    return resultadoConsulta;
                }
            }

            // Sin consulta posible (beta, boletas, series B): se reenvía el mismo ZIP; si SUNAT ya lo tiene responde 1033.
        }

        if (plazos.FechaLimite(referencia.TipoDocumento, null, referencia.Fecha) is { } limite && limite < reloj.HoyLima())
        {
            return soporte.Local(contexto, "PCPE-4006", referencia.NombreArchivo, limite.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
        }

        var resultado = await SendBillAsync(emisor, contexto, servicio!, ct).ConfigureAwait(false);
        if (resultado.Codigo == ClasificadorRespuesta.CodigoRegistradoPreviamente && PuedeConsultar(emisor, referencia, out var consultaTras1033))
        {
            return (await ConsultarConReintentosAsync(emisor, contexto, consultaTras1033, resultado.Estado, ct).ConfigureAwait(false)).Resultado;
        }

        return resultado;
    }

    public async Task<ResultadoEnvio> ConsultarCdrAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(referencia);
        var contexto = new ContextoEnvio(referencia, OperacionSoap.GetStatusCdr, soporte.Rutas(referencia));
        if (soporte.Preparar(emisor, contexto, ServicioSunat.Comprobantes) is { Error: { } errorConfiguracion })
        {
            return errorConfiguracion;
        }

        if (!PuedeConsultar(emisor, referencia, out var consulta))
        {
            var motivo = endpoints.Obtener(emisor.Canal, opciones.Value.Ambiente, ServicioSunat.ConsultaCdr) is null
                ? $"en el ambiente {opciones.Value.Ambiente} del canal {emisor.Canal.Clave}"
                : "para boletas ni para series que no empiezan con F (código 0009/0010 de la consulta)";
            return clasificador.Local(contexto, EstadoCpe.ErrorServicio, errores.Local("PCPE-4005", OrigenError.Modelo, null, null, motivo));
        }

        using var bloqueo = await bloqueos.AdquirirAsync(referencia.NombreArchivo, ct).ConfigureAwait(false);
        var previo = await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false);
        if (await soporte.ConectividadAsync(emisor, contexto, consulta.Url, ct).ConfigureAwait(false) is { } sinConexion)
        {
            return sinConexion;
        }

        return (await ConsultarConReintentosAsync(emisor, contexto, consulta, previo?.EstadoActual, ct).ConfigureAwait(false)).Resultado;
    }

    /// <summary>
    /// El último envío que llegó a SUNAT pudo haber quedado registrado: respuesta incierta, en proceso (0140), fault de
    /// constancia o 1033. Los intentos que no llegaron a SUNAT (sin Internet, sin conexión) se saltan; una consulta
    /// que respondió «no existe» lo resuelve.
    /// </summary>
    private bool RequiereConsultaPrevia(HistorialCpe? previo)
    {
        if (previo is null)
        {
            return false;
        }

        for (var i = previo.Intentos.Count - 1; i >= 0; i--)
        {
            var intento = previo.Intentos[i];
            if (intento.Operacion == OperacionSoap.GetStatusCdr)
            {
                if (respuestas.EstadoConsulta(intento.Codigo)?.Resultado == ResultadoConsultaCdr.NoExiste)
                {
                    return false;
                }

                continue;
            }

            if (intento.Operacion != OperacionSoap.SendBill || intento.Estado == EstadoCpe.SinConexionInternet
                || (intento.Estado == EstadoCpe.ServicioNoDisponible && intento.Codigo is null))
            {
                continue;
            }

            return intento.Estado is EstadoCpe.RespuestaIncierta or EstadoCpe.EnProceso
                || intento.Codigo == ClasificadorRespuesta.CodigoRegistradoPreviamente
                || errores.Buscar(intento.Codigo) is { Transitorio: true, GrupoTransitorio: not "disponibilidad" };
        }

        return false;
    }

    /// <summary>getStatusCdr solo existe donde hay endpoint de consulta y para 01, 07 y 08 con serie F.</summary>
    private bool PuedeConsultar(EmisorCpe emisor, ReferenciaCpe referencia, out EndpointCpe consulta)
    {
        consulta = endpoints.Obtener(emisor.Canal, opciones.Value.Ambiente, ServicioSunat.ConsultaCdr)!;
        return consulta is not null
            && (referencia.TipoDocumento is TipoDocumentoSunat.Factura or TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito)
            && referencia.Serie?.StartsWith('F') == true;
    }

    private async Task<ResultadoEnvio> SendBillAsync(EmisorCpe emisor, ContextoEnvio contexto, EndpointCpe servicio, CancellationToken ct)
    {
        var referencia = contexto.Referencia;
        var zip = await File.ReadAllBytesAsync(contexto.Rutas.Zip!, ct).ConfigureAwait(false);
        var credenciales = emisor.CredencialesSoap!;
        var sobre = ConstructorSobreSoap.SendBill(ConstructorSobreSoap.UsuarioSoap(emisor, credenciales), credenciales.Clave, NombreArchivoCpe.Zip(referencia), Convert.ToBase64String(zip));
        using var actividad = Telemetria.Actividades.StartActivity(OperacionSoap.SendBill);
        actividad?.SetTag("pashecpe.documento", referencia.NombreArchivo);
        var soap = await cliente.LlamarAsync(servicio.Url, OperacionSoap.SendBill, servicio.SoapAction, sobre, ct).ConfigureAwait(false);

        var contenido = lectorSoap.Leer(soap.Cuerpo);
        ResultadoEnvio resultado;
        if (contenido?.ApplicationResponse is { } cdrBase64 && SoporteEnvio.TryBase64(cdrBase64) is { } cdrZip)
        {
            var (cdr, ruta) = await lectorCdr.GuardarYLeerAsync(referencia, cdrZip, ct).ConfigureAwait(false);
            var conCdr = contexto with { Rutas = contexto.Rutas with { Cdr = ruta } };
            resultado = cdr is null ? clasificador.DesdeRed(conCdr, soap with { Falla = FallaRed.Ninguna }, idempotente: false) : clasificador.DesdeCdr(conCdr, soap, cdr);
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
        return await soporte.RegistrarAsync(emisor, referencia, resultado, resultado.Estado, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// getStatusCdr con hasta 3 reintentos ante fallas de red o faults transitorios. Devuelve el resultado y si SUNAT
    /// respondió que no tiene el comprobante («no existe»): en ese caso el estado del historial no cambia.
    /// </summary>
    private async Task<(ResultadoEnvio Resultado, bool NoExiste)> ConsultarConReintentosAsync(EmisorCpe emisor, ContextoEnvio contexto, EndpointCpe consulta, EstadoCpe? estadoPrevio, CancellationToken ct)
    {
        var referencia = contexto.Referencia;
        var contextoConsulta = contexto with { Operacion = OperacionSoap.GetStatusCdr };
        var credenciales = emisor.CredencialesSoap!;
        var sobre = ConstructorSobreSoap.GetStatusCdr(ConstructorSobreSoap.UsuarioSoap(emisor, credenciales), credenciales.Clave, referencia);
        var estadoSinCambio = estadoPrevio ?? EstadoCpe.Generado;
        for (var intento = 0; ; intento++)
        {
            using var actividad = Telemetria.Actividades.StartActivity(OperacionSoap.GetStatusCdr);
            actividad?.SetTag("pashecpe.documento", referencia.NombreArchivo);
            var soap = await cliente.LlamarAsync(consulta.Url, OperacionSoap.GetStatusCdr, consulta.SoapAction, sobre, ct).ConfigureAwait(false);
            var (resultado, noExiste) = await InterpretarConsultaAsync(contextoConsulta, soap, estadoSinCambio, ct).ConfigureAwait(false);
            var concluyente = !noExiste && resultado.Estado is not (EstadoCpe.ServicioNoDisponible or EstadoCpe.ErrorServicio);
            resultado = await soporte.RegistrarAsync(emisor, referencia, resultado, concluyente ? resultado.Estado : estadoSinCambio, ct).ConfigureAwait(false);
            if (resultado.Estado != EstadoCpe.ServicioNoDisponible || intento >= esperas.Esperas.Count)
            {
                return (resultado, noExiste);
            }

            RegistroEventos.Reintento(registro, intento + 1, OperacionSoap.GetStatusCdr, referencia.NombreArchivo, resultado.Estado, resultado.Codigo);
            await Task.Delay(esperas.Esperas[intento], tiempo, ct).ConfigureAwait(false);
        }
    }

    private async Task<(ResultadoEnvio Resultado, bool NoExiste)> InterpretarConsultaAsync(ContextoEnvio contexto, RespuestaSoap soap, EstadoCpe estadoSinCambio, CancellationToken ct)
    {
        var contenido = lectorSoap.Leer(soap.Cuerpo);
        if (contenido?.Fault is { } fault)
        {
            return (clasificador.DesdeFault(contexto, soap, fault), false);
        }

        if (contenido?.StatusCdr is not { } estado)
        {
            soporte.InvalidarConectividad(soap.Endpoint, soap);
            return (clasificador.DesdeRed(contexto, soap, idempotente: true), false);
        }

        if (estado.ContenidoBase64 is { } cdrBase64 && SoporteEnvio.TryBase64(cdrBase64) is { } cdrZip)
        {
            var (cdr, ruta) = await lectorCdr.GuardarYLeerAsync(contexto.Referencia, cdrZip, ct).ConfigureAwait(false);
            if (cdr is not null)
            {
                return (clasificador.DesdeCdr(contexto with { Rutas = contexto.Rutas with { Cdr = ruta } }, soap, cdr), false);
            }
        }

        if (respuestas.EstadoConsulta(estado.Codigo) is not { } consulta)
        {
            return (clasificador.Local(contexto, EstadoCpe.ErrorServicio, errores.Local("PCPE-4008", OrigenError.Sunat, null, null, estado.Codigo, estado.Mensaje)), false);
        }

        return (clasificador.DesdeConsulta(contexto, soap, consulta, estado.Mensaje, estadoSinCambio), consulta.Resultado == ResultadoConsultaCdr.NoExiste);
    }

    private static bool EsBoleta(ReferenciaCpe referencia) =>
        referencia.TipoDocumento == TipoDocumentoSunat.BoletaDeVenta
        || ((referencia.TipoDocumento is TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito) && referencia.Serie?.StartsWith('B') == true);
}
