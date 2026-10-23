using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Resumen;

/// <summary>Envío del resumen diario (sendSummary) y ciclo del ticket (getStatus) contra el SUNAT falso: sin red.</summary>
public sealed class EnvioResumenTests : IDisposable
{
    private const string _ticket = "1729876543210";
    private static readonly DateOnly _fecha = new(2026, 10, 22);

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnviarAsync_Resumen_DevuelveElTicketYDejaEnProcesoAlResumenYASusBoletas()
    {
        var (emisor, resumen) = await GenerarResumenAsync(_servicios);
        _servicios.Sunat.Responder(SendSummary, Ticket(_ticket));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, resumen, Ct);

        Assert.Equal((EstadoCpe.EnProceso, _ticket, true), (resultado.Estado, resultado.Ticket, resultado.RequiereConsultaEstado));
        Assert.StartsWith("SUNAT está procesando el resumen", resultado.MensajeUsuario, StringComparison.Ordinal);
        var solicitud = Assert.Single(_servicios.Sunat.Solicitudes);
        Assert.Equal("\"urn:sendSummary\"", solicitud.SoapAction);
        Assert.Contains("<fileName>20123456786-RC-20261022-1.zip</fileName>", solicitud.Cuerpo, StringComparison.Ordinal);
        var historial = await Historial(_servicios, resumen);
        Assert.Equal((EstadoCpe.EnProceso, _ticket), (historial.EstadoActual, historial.Ticket));
        foreach (var numero in new long[] { 1, 2 })
        {
            var boleta = await Historial(_servicios, Boleta(numero));
            Assert.Equal((EstadoCpe.EnProceso, _ticket), (boleta.EstadoActual, boleta.Ticket));
            Assert.Equal(OperacionSendSummary, boleta.Intentos[^1].Operacion);
            Assert.StartsWith("20123456786-RC-20261022-1:", boleta.Intentos[^1].Descripcion, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EsperarTicketAsync_EnProcesoYLuegoAceptado_GuardaElCdrYAceptaLasBoletas()
    {
        var (emisor, resumen) = await EnviarResumenAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("98")).Responder(GetStatus, Status("98")).Responder(GetStatus, Status("0", CdrDoradoResumen()));

        var resultado = await ConReloj(_servicios, Servicio(_servicios).EsperarTicketAsync(emisor, resumen, ct: Ct));

        Assert.Equal((EstadoCpe.Aceptado, "0", _ticket), (resultado.Estado, resultado.Codigo, resultado.Ticket));
        Assert.Equal(3, _servicios.Sunat.Llamadas(GetStatus));
        Assert.Contains($"<ticket>{_ticket}</ticket>", _servicios.Sunat.Solicitudes[^1].Cuerpo, StringComparison.Ordinal);
        Assert.Equal("\"urn:getStatus\"", _servicios.Sunat.Solicitudes[^1].SoapAction);
        Assert.Equal(Path.Combine(_servicios.Almacen.Ruta, "RPTA", "2026", "10", "R-20123456786-RC-20261022-1.zip"), resultado.Rutas.Cdr);
        Assert.True(File.Exists(resultado.Rutas.Cdr));
        Assert.Equal(EstadoCpe.Aceptado, (await Historial(_servicios, resumen)).EstadoActual);
        Assert.Equal(EstadoCpe.Aceptado, (await Historial(_servicios, Boleta(1))).EstadoActual);
        Assert.Equal(EstadoCpe.Aceptado, (await Historial(_servicios, Boleta(2))).EstadoActual);
    }

    [Fact]
    public async Task ConsultarTicketAsync_99ConCdrDeRechazo_RechazaYLasBoletasVuelvenAGenerado()
    {
        var (emisor, resumen) = await EnviarResumenAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("99", Cdr("RC-20261022-1", "2282", "El comprobante ya fue informado")));

        var resultado = await Servicio(_servicios).ConsultarTicketAsync(emisor, resumen, _ticket, Ct);

        Assert.Equal((EstadoCpe.Rechazado, "2282"), (resultado.Estado, resultado.Codigo));
        Assert.Equal(EstadoCpe.Rechazado, (await Historial(_servicios, resumen)).EstadoActual);
        var boleta = await Historial(_servicios, Boleta(1));
        Assert.Equal(EstadoCpe.Generado, boleta.EstadoActual);
        Assert.Equal(EstadoCpe.Rechazado, boleta.Intentos[^1].Estado);
    }

    [Fact]
    public async Task ConsultarTicketAsync_BoletaVueltaAGenerado_SePuedeInformarEnOtroResumen()
    {
        var (emisor, resumen) = await EnviarResumenAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("99"));
        var rechazo = await Servicio(_servicios).ConsultarTicketAsync(emisor, resumen, ct: Ct);

        var otro = await Servicio(_servicios).GenerarResumenAsync(emisor, _fecha, [GeneracionResumenTests.Documento(1), GeneracionResumenTests.Documento(2)], Ct);

        Assert.Equal((EstadoCpe.Rechazado, "99"), (rechazo.Estado, rechazo.Codigo));
        Assert.Contains(rechazo.Errores, e => e.Codigo == "PCPE-4013");
        var resultado = Assert.Single(otro);
        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        Assert.Equal("20123456786-RC-20261022-2", resultado.NombreArchivo);
    }

    [Fact]
    public async Task ConsultarTicketAsync_TicketInexistente_ErrorDeServicioSinCambiarElEstado()
    {
        var (emisor, resumen) = await EnviarResumenAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Fault("soap-env:Client.0127", "El ticket no existe"));

        var resultado = await Servicio(_servicios).ConsultarTicketAsync(emisor, resumen, ct: Ct);

        Assert.Equal((EstadoCpe.ErrorServicio, "0127"), (resultado.Estado, resultado.Codigo));
        Assert.Equal(EstadoCpe.EnProceso, (await Historial(_servicios, resumen)).EstadoActual);
    }

    [Fact]
    public async Task EsperarTicketAsync_SigueEnProceso_DevuelveEnProcesoConElTicketTrasLasEsperas()
    {
        using var servicios = new ServiciosPrueba(o =>
        {
            o.Tickets.EsperasSegundos.Add(1);
            o.Tickets.EsperasSegundos.Add(2);
        });
        var (emisor, resumen) = await EnviarResumenAsync(servicios);
        servicios.Sunat.Responder(GetStatus, Status("98")).Responder(GetStatus, Status("98"));

        var resultado = await ConReloj(servicios, Servicio(servicios).EsperarTicketAsync(emisor, resumen, ct: Ct));

        Assert.Equal((EstadoCpe.EnProceso, _ticket, true), (resultado.Estado, resultado.Ticket, resultado.RequiereConsultaEstado));
        Assert.Equal(2, servicios.Sunat.Llamadas(GetStatus));
    }

    [Fact]
    public async Task EnviarAsync_FaultDeDisponibilidad_ReintentaSendSummary()
    {
        var (emisor, resumen) = await GenerarResumenAsync(_servicios);
        _servicios.Sunat.Responder(SendSummary, Fault("soap-env:Server.0100", "El sistema no puede responder su solicitud")).Responder(SendSummary, Ticket(_ticket));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, resumen, Ct);

        Assert.Equal((EstadoCpe.EnProceso, _ticket), (resultado.Estado, resultado.Ticket));
        Assert.Equal(2, _servicios.Sunat.Llamadas(SendSummary));
    }

    [Fact]
    public async Task EnviarAsync_FaultDeConstancia_NoReintentaSendSummary()
    {
        var (emisor, resumen) = await GenerarResumenAsync(_servicios);
        _servicios.Sunat.Responder(SendSummary, Fault("soap-env:Server.0130", "El sistema no pudo obtener el ticket"));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, resumen, Ct);

        Assert.Equal((EstadoCpe.ServicioNoDisponible, "0130", true), (resultado.Estado, resultado.Codigo, resultado.EsReintentable));
        Assert.Equal(1, _servicios.Sunat.Llamadas(SendSummary));
        Assert.Equal(EstadoCpe.Generado, (await Historial(_servicios, Boleta(1))).EstadoActual);
    }

    [Fact]
    public async Task EnviarAsync_ResumenEnProceso_ConsultaElTicketYNoReenvia()
    {
        var (emisor, resumen) = await EnviarResumenAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("0", CdrDoradoResumen()));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, resumen, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal((1, 1), (_servicios.Sunat.Llamadas(SendSummary), _servicios.Sunat.Llamadas(GetStatus)));
    }

    [Fact]
    public async Task EnviarAsync_ResumenAceptado_DevuelveElHistorialSinLlamarASunat()
    {
        var (emisor, resumen) = await EnviarResumenAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("0", CdrDoradoResumen()));
        await Servicio(_servicios).ConsultarTicketAsync(emisor, resumen, ct: Ct);
        var llamadas = _servicios.Sunat.Solicitudes.Count;

        var reenvio = await Servicio(_servicios).EnviarAsync(emisor, resumen, Ct);
        var consulta = await Servicio(_servicios).ConsultarTicketAsync(emisor, resumen, ct: Ct);

        Assert.Equal(EstadoCpe.Aceptado, reenvio.Estado);
        Assert.Equal((EstadoCpe.Aceptado, _ticket), (consulta.Estado, consulta.Ticket));
        Assert.Equal(llamadas, _servicios.Sunat.Solicitudes.Count);
    }

    [Fact]
    public async Task ConsultarTicketAsync_DesdeOtroProceso_TomaElTicketDelHistorial()
    {
        var (emisor, resumen) = await EnviarResumenAsync(_servicios);
        using var otroProceso = new ServiciosPrueba(o => o.RutaBase = _servicios.Almacen.Ruta);
        otroProceso.Sunat.Responder(GetStatus, Status("0", CdrDoradoResumen()));

        var resultado = await Servicio(otroProceso).ConsultarTicketAsync(emisor, resumen, ct: Ct);

        Assert.Equal((EstadoCpe.Aceptado, _ticket), (resultado.Estado, resultado.Ticket));
        Assert.Contains($"<ticket>{_ticket}</ticket>", otroProceso.Sunat.Solicitudes[^1].Cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConsultarTicketAsync_SinTicket_ErrorLocalSinLlamarASunat()
    {
        var (emisor, resumen) = await GenerarResumenAsync(_servicios);

        var resultado = await Servicio(_servicios).ConsultarTicketAsync(emisor, resumen, ct: Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, "PCPE-4011"), (resultado.Estado, resultado.Codigo));
        Assert.Empty(_servicios.Sunat.Solicitudes);
    }

    [Fact]
    public async Task ConsultarTicketAsync_ConLaReferenciaDeUnaBoleta_ErrorLocalSinLlamarASunat()
    {
        var (emisor, _) = await EnviarResumenAsync(_servicios);
        var llamadas = _servicios.Sunat.Solicitudes.Count;

        var resultado = await Servicio(_servicios).ConsultarTicketAsync(emisor, Boleta(1), ct: Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, "PCPE-4015"), (resultado.Estado, resultado.Codigo));
        Assert.Equal(llamadas, _servicios.Sunat.Solicitudes.Count);
    }

    [Fact]
    public async Task GenerarAsync_BoletaInformadaEnResumen_NoSeRegenera()
    {
        var (emisor, _) = await EnviarResumenAsync(_servicios);

        var regenerada = await Servicio(_servicios).GenerarAsync(emisor, BoletaCpe(1, AfectacionIgv.GravadoOneroso), Ct);

        Assert.Equal("PCPE-3101", Assert.Single(regenerada.Errores).Codigo);
    }

    [Fact]
    public async Task EnviarAsync_BoletaDelResumenEnModoIndividual_NoSeVuelveAEnviar()
    {
        using var servicios = new ServiciosPrueba(o => o.ModoEnvioBoletas = ModoEnvioBoletas.Individual);
        var (emisor, resumen) = await EnviarResumenAsync(servicios);
        servicios.Sunat.Responder(GetStatus, Status("0", CdrDoradoResumen()));
        await Servicio(servicios).ConsultarTicketAsync(emisor, resumen, ct: Ct);

        var envio = await Servicio(servicios).EnviarAsync(emisor, Boleta(1), Ct);

        Assert.Equal(EstadoCpe.Aceptado, envio.Estado);
        Assert.Equal(0, servicios.Sunat.Llamadas(SendBill));
    }

    private const string OperacionSendSummary = "sendSummary";

    private static IServicioCpe Servicio(ServiciosPrueba servicios) => servicios.Obtener<IServicioCpe>();

    private static ReferenciaCpe Boleta(long numero) =>
        ReferenciaCpe.Comprobante(Ejemplos.Ruc, TipoDocumentoSunat.BoletaDeVenta, "B001", numero, _fecha);

    private static async Task<HistorialCpe> Historial(ServiciosPrueba servicios, ReferenciaCpe referencia) =>
        (await servicios.Obtener<IHistorialCpe>().ObtenerAsync(referencia, Ct))!;

    private static ComprobanteCpe BoletaCpe(long numero, string afectacion) =>
        CasosTributarios.Boleta(CasosTributarios.Linea(1, 59m) with { Afectacion = afectacion }) with { Numero = numero };

    /// <summary>Dos boletas (gravada y exonerada) y su resumen generado desde FIRMA.</summary>
    internal static async Task<(EmisorCpe Emisor, ReferenciaCpe Resumen)> GenerarResumenAsync(ServiciosPrueba servicios)
    {
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        foreach (var (numero, afectacion) in new[] { (1L, AfectacionIgv.GravadoOneroso), (2L, AfectacionIgv.ExoneradoOneroso) })
        {
            var boleta = await servicios.Obtener<IServicioCpe>().GenerarAsync(emisor, BoletaCpe(numero, afectacion), Ct);
            Assert.True(boleta.Exito, GeneracionResumenTests.Errores(boleta));
        }

        var resultado = Assert.Single(await servicios.Obtener<IServicioCpe>().GenerarResumenAsync(emisor, _fecha, [GeneracionResumenTests.Documento(1), GeneracionResumenTests.Documento(2)], Ct));
        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        return (emisor, resultado.Referencia);
    }

    private static async Task<(EmisorCpe Emisor, ReferenciaCpe Resumen)> EnviarResumenAsync(ServiciosPrueba servicios)
    {
        var (emisor, resumen) = await GenerarResumenAsync(servicios);
        servicios.Sunat.Responder(SendSummary, Ticket(_ticket));
        var envio = await servicios.Obtener<IServicioCpe>().EnviarAsync(emisor, resumen, Ct);
        Assert.Equal(EstadoCpe.EnProceso, envio.Estado);
        return (emisor, resumen);
    }

    /// <summary>Avanza el reloj falso mientras la tarea espera (las esperas del ticket usan TimeProvider).</summary>
    private static async Task<T> ConReloj<T>(ServiciosPrueba servicios, Task<T> tarea)
    {
        for (var vueltas = 0; !tarea.IsCompleted && vueltas < 1000; vueltas++)
        {
            servicios.Reloj.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, Ct);
        }

        return await tarea;
    }
}
