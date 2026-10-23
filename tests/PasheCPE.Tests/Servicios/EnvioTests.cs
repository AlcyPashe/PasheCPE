using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Transporte;
using PasheCPE.Transporte.Soap;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Servicios;

public sealed class EnvioTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnviarAsync_CdrAceptado_AceptadoConCdrEnRptaEHistorial()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.Aceptado, 3, "0"), (resultado.Estado, resultado.Nivel, resultado.Codigo));
        Assert.Equal("Comprobante aceptado por SUNAT.", resultado.MensajeUsuario);
        Assert.Equal(Path.Combine(_servicios.Almacen.Ruta, "RPTA", "2026", "10", "R-20123456786-01-F001-123.zip"), resultado.Rutas.Cdr);
        Assert.True(File.Exists(resultado.Rutas.Cdr));
        Assert.False(string.IsNullOrEmpty(resultado.ValorResumen));
        Assert.Equal(["generar", "sendBill"], resultado.Historial.Select(i => i.Operacion));
        Assert.Equal(EstadoCpe.Aceptado, (await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(referencia, Ct))!.EstadoActual);

        var solicitud = Assert.Single(_servicios.Sunat.Solicitudes);
        Assert.Equal("https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService", solicitud.Url.ToString());
        Assert.Equal("\"urn:sendBill\"", solicitud.SoapAction);
        Assert.Equal("text/xml; charset=utf-8", solicitud.ContentType);
        Assert.Contains("<fileName>20123456786-01-F001-123.zip</fileName>", solicitud.Cuerpo, StringComparison.Ordinal);
        Assert.Contains($"<contentFile>{Convert.ToBase64String(await File.ReadAllBytesAsync(resultado.Rutas.Zip!, Ct))}</contentFile>", solicitud.Cuerpo, StringComparison.Ordinal);
        Assert.Contains("<wsse:Username>20123456786MODDATOS</wsse:Username>", solicitud.Cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarAsync_CdrConNotas_AceptadoConObservaciones()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "0", "La Factura numero F001-123, ha sido aceptada", "4252 - El dato ingresado como atributo @listName es incorrecto.")));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.AceptadoConObservaciones, resultado.Estado);
        var observacion = Assert.Single(resultado.Observaciones);
        Assert.Equal("4252", observacion.Codigo);
    }

    [Fact]
    public async Task EnviarAsync_CdrRechazado_RechazadoYNoSeVuelveAEnviar()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "2800", "El dato ingresado en el tipo de documento de identidad del receptor no esta permitido.")));

        var primero = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);
        var segundo = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.Rechazado, false), (primero.Estado, primero.EsReintentable));
        Assert.Equal("2800", Assert.Single(primero.Errores).Codigo);
        Assert.Equal((EstadoCpe.Rechazado, "2800"), (segundo.Estado, segundo.Codigo));
        Assert.Equal(1, _servicios.Sunat.Llamadas(SendBill));
    }

    [Fact]
    public async Task EnviarAsync_FaultDeRechazo2xxx_Rechazado()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, Fault("soap-env:Client.2335", "El documento electrónico ingresado ha sido alterado"), HttpStatusCode.InternalServerError);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.Rechazado, "2335"), (resultado.Estado, resultado.Codigo));
        Assert.Equal("soap-env:Client.2335", resultado.Tecnico!.FaultCode);
        Assert.Equal(500, resultado.Tecnico.Http);
    }

    [Fact]
    public async Task EnviarAsync_Fault0111_ErrorServicioConMensajeDelCodigo()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, Fault("soap-env:Client.0111", "No tiene el perfil para enviar comprobantes electronicos"), HttpStatusCode.InternalServerError);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ErrorServicio, 2, false, false), (resultado.Estado, resultado.Nivel, resultado.EsReintentable, resultado.RequiereConsultaEstado));
        Assert.Contains("perfil", resultado.MensajeUsuario, StringComparison.Ordinal);
        Assert.Equal("No tiene el perfil para enviar comprobantes electronicos", resultado.DescripcionCatalogo);
    }

    [Theory]
    [InlineData("0100")]
    [InlineData("0109")]
    public async Task EnviarAsync_FaultDeDisponibilidad_ReintentableSinReintentoInternoYReenvioDirecto(string codigo)
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Responder(SendBill, Fault($"soap-env:Client.{codigo}", "El sistema no puede responder su solicitud"), HttpStatusCode.InternalServerError);
        servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));

        var primero = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);
        Assert.Equal((EstadoCpe.ServicioNoDisponible, true, false), (primero.Estado, primero.EsReintentable, primero.RequiereConsultaEstado));
        Assert.Equal(1, servicios.Sunat.Llamadas(SendBill));

        var segundo = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, segundo.Estado);
        Assert.Equal(0, servicios.Sunat.Llamadas(GetStatusCdr));
    }

    [Fact]
    public async Task EnviarAsync_Fault0140_EnProcesoConConsulta()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, Fault("soap-env:Client.0140", "Existe un Documento igual en Proceso."), HttpStatusCode.InternalServerError);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.EnProceso, true, false), (resultado.Estado, resultado.RequiereConsultaEstado, resultado.EsReintentable));
    }

    [Fact]
    public async Task EnviarAsync_Fault1033EnBeta_ErrorServicioQuePideVerificarEnSol()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, Fault("soap-env:Client.1033", "El comprobante fue registrado previamente con otros datos"), HttpStatusCode.InternalServerError);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ErrorServicio, "1033", true), (resultado.Estado, resultado.Codigo, resultado.RequiereConsultaEstado));
        Assert.Contains("SUNAT Operaciones en Línea", resultado.MensajeUsuario, StringComparison.Ordinal);
        Assert.Equal(0, _servicios.Sunat.Llamadas(GetStatusCdr));
    }

    [Fact]
    public async Task EnviarAsync_Fault1033EnProduccion_ConsultaElCdrYDevuelveElEstadoReal()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Responder(SendBill, Fault("soap-env:Client.1033", "El comprobante fue registrado previamente con otros datos"), HttpStatusCode.InternalServerError);
        servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0001", "La constancia existe", CdrDoradoAceptado()));

        var resultado = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal(["generar", "sendBill", "getStatusCdr"], resultado.Historial.Select(i => i.Operacion));
        var consulta = servicios.Sunat.Solicitudes.Single(s => s.Operacion == GetStatusCdr);
        Assert.Equal("https://e-factura.sunat.gob.pe/ol-it-wsconscpegem/billConsultService", consulta.Url.ToString());
        Assert.Equal("\"urn:getStatusCdr\"", consulta.SoapAction);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task EnviarAsync_Http5xxDeDisponibilidad_ServicioNoDisponible(HttpStatusCode http)
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, "<html>Servicio no disponible</html>", http);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ServicioNoDisponible, 0, true), (resultado.Estado, resultado.Nivel, resultado.EsReintentable));
        Assert.Equal((int)http, resultado.Tecnico!.Http);
    }

    [Fact]
    public async Task EnviarAsync_TimeoutDeConexion_ServicioNoDisponible()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ConnectionError, "No se pudo conectar", new SocketException((int)SocketError.TimedOut)));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ServicioNoDisponible, true, false), (resultado.Estado, resultado.EsReintentable, resultado.RequiereConsultaEstado));
        Assert.Contains("SocketException", resultado.Tecnico!.Excepcion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarAsync_TimeoutDeRespuesta_RespuestaIncierta()
    {
        using var servicios = new ServiciosPrueba(o => o.TimeoutRespuestaSegundos = 1);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Colgar(SendBill);

        var resultado = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.RespuestaIncierta, false, true), (resultado.Estado, resultado.EsReintentable, resultado.RequiereConsultaEstado));
        Assert.Contains("No lo vuelva a emitir", resultado.MensajeUsuario, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarAsync_ConexionCortada_RespuestaIncierta()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ResponseEnded, "La respuesta terminó antes de tiempo"));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.RespuestaIncierta, resultado.Estado);
    }

    [Fact]
    public async Task EnviarAsync_SinInternet_NoLlamaYQuedaPendiente()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Red.HayInterfaz = false;

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.SinConexionInternet, 0, true), (resultado.Estado, resultado.Nivel, resultado.EsReintentable));
        Assert.Empty(_servicios.Sunat.Solicitudes);
        var pendiente = Assert.Single(await _servicios.Obtener<IHistorialCpe>().ListarPendientesAsync(2026, 10, Ct).ToListAsync(Ct));
        Assert.Equal(EstadoCpe.SinConexionInternet, pendiente.EstadoActual);
    }

    [Fact]
    public async Task EnviarAsync_SunatNoRespondeYHayInternet_ServicioNoDisponible()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Red.Host("e-beta.sunat.gob.pe", responde: false);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.ServicioNoDisponible, resultado.Estado);
        Assert.Empty(_servicios.Sunat.Solicitudes);
    }

    [Fact]
    public async Task EnviarAsync_TrasRespuestaInciertaEnProduccion_ConsultaYNoReenvia()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"));
        servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0001", "La constancia existe", CdrDoradoAceptado()));
        Assert.Equal(EstadoCpe.RespuestaIncierta, (await Servicio(servicios).EnviarAsync(emisor, referencia, Ct)).Estado);

        var resultado = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal(1, servicios.Sunat.Llamadas(SendBill));
        Assert.True(File.Exists(resultado.Rutas.Cdr));
    }

    [Fact]
    public async Task EnviarAsync_TrasRespuestaInciertaYConsultaNoExiste_Reenvia()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"));
        servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0011", "El comprobante de pago electrónico no existe."));
        servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));
        await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        var resultado = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal((2, 1), (servicios.Sunat.Llamadas(SendBill), servicios.Sunat.Llamadas(GetStatusCdr)));
        Assert.Equal(["generar", "sendBill", "getStatusCdr", "sendBill"], resultado.Historial.Select(i => i.Operacion));
    }

    [Fact]
    public async Task EnviarAsync_TrasRespuestaInciertaEnBeta_ReenviaElMismoZip()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"));
        _servicios.Sunat.Responder(SendBill, Fault("soap-env:Client.1033", "El comprobante fue registrado previamente con otros datos"), HttpStatusCode.InternalServerError);
        await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ErrorServicio, "1033", true), (resultado.Estado, resultado.Codigo, resultado.RequiereConsultaEstado));
        Assert.Equal(0, _servicios.Sunat.Llamadas(GetStatusCdr));
        var envios = _servicios.Sunat.Solicitudes.Where(s => s.Operacion == SendBill).Select(s => s.Cuerpo).ToList();
        Assert.Equal(2, envios.Count);
        Assert.Equal(envios[0], envios[1]);
    }

    [Fact]
    public async Task EnviarAsync_TrasFaultDeConstancia_ConsultaAntesDeReenviar()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Responder(SendBill, Fault("soap-env:Client.0125", "No se pudo obtener la constancia"), HttpStatusCode.InternalServerError);
        servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0001", "La constancia existe", CdrDoradoAceptado()));
        var primero = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);
        Assert.Equal((EstadoCpe.ServicioNoDisponible, true), (primero.Estado, primero.RequiereConsultaEstado));

        var segundo = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, segundo.Estado);
        Assert.Equal(1, servicios.Sunat.Llamadas(SendBill));
    }

    [Fact]
    public async Task ConsultarCdrAsync_FaultTransitorio_ReintentaHasta3Veces()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Responder(GetStatusCdr, Fault("soap-env:Client.0100", "El sistema no puede responder su solicitud"), HttpStatusCode.InternalServerError);
        servicios.Sunat.Lanzar(GetStatusCdr, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"));
        servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0001", "La constancia existe", CdrDoradoAceptado()));

        var resultado = await Servicio(servicios).ConsultarCdrAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal(3, servicios.Sunat.Llamadas(GetStatusCdr));
    }

    [Fact]
    public async Task ConsultarCdrAsync_FallaPersistente_MaximoTresReintentos()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        for (var i = 0; i < 5; i++)
        {
            servicios.Sunat.Responder(GetStatusCdr, "<html/>", HttpStatusCode.ServiceUnavailable);
        }

        var resultado = await Servicio(servicios).ConsultarCdrAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ServicioNoDisponible, true), (resultado.Estado, resultado.EsReintentable));
        Assert.Equal(4, servicios.Sunat.Llamadas(GetStatusCdr));
        Assert.Equal(EstadoCpe.Generado, (await servicios.Obtener<IHistorialCpe>().ObtenerAsync(referencia, Ct))!.EstadoActual);
    }

    [Fact]
    public async Task ConsultarCdrAsync_NoExiste_ConservaElEstadoDelHistorial()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0011", "El comprobante de pago electrónico no existe."));

        var resultado = await Servicio(servicios).ConsultarCdrAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.Generado, "0011", true), (resultado.Estado, resultado.Codigo, resultado.EsReintentable));
    }

    [Fact]
    public async Task ConsultarCdrAsync_EnBeta_NoSoportadoSinLlamar()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);

        var resultado = await Servicio(_servicios).ConsultarCdrAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ErrorServicio, "PCPE-4005"), (resultado.Estado, resultado.Codigo));
        Assert.Empty(_servicios.Sunat.Solicitudes);
    }

    [Fact]
    public async Task EnviarAsync_DosALaVez_UnSoloSendBill()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        var liberar = new TaskCompletionSource();
        _servicios.Sunat.ResponderTras(SendBill, liberar.Task, ConCdr(CdrDoradoAceptado()));

        var primero = Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);
        var segundo = Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);
        liberar.SetResult();
        var resultados = await Task.WhenAll(primero, segundo);

        Assert.All(resultados, r => Assert.Equal(EstadoCpe.Aceptado, r.Estado));
        Assert.Equal(1, _servicios.Sunat.Llamadas(SendBill));
        Assert.Equal(0, _servicios.Obtener<BloqueoDocumentos>().Activos);
    }

    [Fact]
    public async Task EnviarAsync_HistorialYLogSinCredenciales()
    {
        var registro = new RegistroFalso<ClienteSoap>();
        using var servicios = new ServiciosPrueba(registrar: s => s.AddSingleton<ILogger<ClienteSoap>>(registro));
        var (emisor, referencia) = await GenerarAsync(servicios);
        servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));

        var resultado = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        var historial = await File.ReadAllTextAsync(resultado.Rutas.Historial!, Ct);
        Assert.DoesNotContain(emisor.CredencialesSoap!.Clave, historial, StringComparison.Ordinal);
        Assert.Contains(registro.Eventos, e => e.Nivel == LogLevel.Debug && e.Mensaje.Contains("<wsse:Password>***</wsse:Password>", StringComparison.Ordinal));
        Assert.DoesNotContain(registro.Eventos, e => e.Mensaje.Contains(emisor.CredencialesSoap.Clave, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnviarAsync_SinGenerar_ErrorLocal4001()
    {
        var resultado = await Servicio(_servicios).EnviarAsync(Ejemplos.Emisor(), Ejemplos.Referencia(), Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, 1, "PCPE-4001"), (resultado.Estado, resultado.Nivel, resultado.Codigo));
        Assert.Empty(_servicios.Sunat.Solicitudes);
    }

    [Fact]
    public async Task EnviarAsync_SinCredencialesSoap_ErrorLocal4003()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor with { CredencialesSoap = null }, referencia, Ct);

        Assert.Equal("PCPE-4003", resultado.Codigo);
    }

    [Fact]
    public async Task EnviarAsync_PlazoVencido_ErrorLocal4006()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Reloj.Advance(TimeSpan.FromDays(10));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal("PCPE-4006", resultado.Codigo);
        Assert.Empty(_servicios.Sunat.Solicitudes);
    }

    [Fact]
    public async Task EnviarAsync_BoletaConResumenDiario_ErrorLocal4002()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios, Boleta());

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal("PCPE-4002", resultado.Codigo);
    }

    [Fact]
    public async Task EnviarAsync_BoletaIndividual_SeEnvia()
    {
        using var servicios = new ServiciosPrueba(o => o.ModoEnvioBoletas = ModoEnvioBoletas.Individual);
        var (emisor, referencia) = await GenerarAsync(servicios, Boleta());
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("B001-123", "0", "La Boleta numero B001-123, ha sido aceptada")));

        var resultado = await Servicio(servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
    }

    [Fact]
    public async Task EmitirAsync_GeneraYEnvia()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        _servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));

        var emision = await Servicio(_servicios).EmitirAsync(emisor, Ejemplos.Factura(), Ct);

        Assert.True(emision.Generacion.Exito);
        Assert.Equal(EstadoCpe.Aceptado, emision.Envio.Estado);
        Assert.Equal(emision.Generacion.ContenidoQr, emision.Envio.ContenidoQr);
        Assert.Equal(emision.Generacion.ValorResumen, emision.Envio.ValorResumen);
    }

    [Fact]
    public async Task EmitirAsync_ErrorDeValidacion_NoEnvia()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var emision = await Servicio(_servicios).EmitirAsync(emisor, Ejemplos.Factura() with { Serie = "B001" }, Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, "PCPE-1204"), (emision.Envio.Estado, emision.Envio.Codigo));
        Assert.Empty(_servicios.Sunat.Solicitudes);
    }

    private static IServicioCpe Servicio(ServiciosPrueba servicios) => servicios.Obtener<IServicioCpe>();

    private static ComprobanteCpe Boleta() => Ejemplos.Factura() with
    {
        TipoDocumento = TipoDocumentoSunat.BoletaDeVenta,
        Serie = "B001",
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "CLIENTE DE PRUEBA" },
        FormaPago = FormaPagoCpe.Contado,
    };

    private static async Task<(EmisorCpe Emisor, ReferenciaCpe Referencia)> GenerarAsync(ServiciosPrueba servicios, ComprobanteCpe? cpe = null)
    {
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        var generacion = await servicios.Obtener<IServicioCpe>().GenerarAsync(emisor, cpe ?? Ejemplos.Factura(), Ct);
        Assert.True(generacion.Exito, string.Join("; ", generacion.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
        return (emisor, generacion.Referencia);
    }
}
