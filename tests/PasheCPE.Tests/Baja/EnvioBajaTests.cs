using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Baja.GeneracionBajaTests;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Baja;

/// <summary>Envío de la comunicación de baja (sendSummary), ciclo del ticket (getStatus) y estado DeBaja. Sin red.</summary>
public sealed class EnvioBajaTests : IDisposable
{
    private const string _ticket = "1729876500001";

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnviarAsync_Baja_DevuelveElTicketYLaFacturaSigueAceptada()
    {
        var (emisor, baja) = await GenerarBajaAsync(_servicios);
        _servicios.Sunat.Responder(SendSummary, Ticket(_ticket));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, baja, Ct);

        Assert.Equal((EstadoCpe.EnProceso, _ticket, true), (resultado.Estado, resultado.Ticket, resultado.RequiereConsultaEstado));
        var solicitud = Assert.Single(_servicios.Sunat.Solicitudes, s => s.Operacion == SendSummary);
        Assert.Equal("\"urn:sendSummary\"", solicitud.SoapAction);
        Assert.Contains("<fileName>20123456786-RA-20261022-1.zip</fileName>", solicitud.Cuerpo, StringComparison.Ordinal);
        Assert.Equal((EstadoCpe.EnProceso, _ticket), ((await Historial(_servicios, baja)).EstadoActual, (await Historial(_servicios, baja)).Ticket));
        var factura = await Historial(_servicios, Factura(123));
        Assert.Equal((EstadoCpe.Aceptado, baja), (factura.EstadoActual, factura.Baja));
        Assert.StartsWith("20123456786-RA-20261022-1:", factura.Intentos[^1].Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EsperarTicketAsync_EnProcesoYLuegoAceptada_GuardaElCdrYLaFacturaQuedaDeBaja()
    {
        var (emisor, baja) = await EnviarBajaAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("98")).Responder(GetStatus, Status("0", CdrBaja("0", "La Comunicacion de baja RA-20261022-1, ha sido aceptada")));

        var resultado = await ConReloj(_servicios, Servicio(_servicios).EsperarTicketAsync(emisor, baja, ct: Ct));

        Assert.Equal((EstadoCpe.Aceptado, "0", _ticket), (resultado.Estado, resultado.Codigo, resultado.Ticket));
        Assert.Equal(2, _servicios.Sunat.Llamadas(GetStatus));
        Assert.Equal(Path.Combine(_servicios.Almacen.Ruta, "RPTA", "2026", "10", "R-20123456786-RA-20261022-1.zip"), resultado.Rutas.Cdr);
        Assert.True(File.Exists(resultado.Rutas.Cdr));
        Assert.Equal(EstadoCpe.Aceptado, (await Historial(_servicios, baja)).EstadoActual);
        var factura = await Historial(_servicios, Factura(123));
        Assert.Equal((EstadoCpe.DeBaja, baja), (factura.EstadoActual, factura.Baja));
        Assert.Equal(EstadoCpe.DeBaja, factura.Intentos[^1].Estado);
    }

    [Fact]
    public async Task ConsultarTicketAsync_99ConCdrDeRechazo_LaFacturaSigueAceptadaYSePuedeInformarEnOtraRa()
    {
        var (emisor, baja) = await EnviarBajaAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("99", CdrBaja("2105", "Comprobante a dar de baja no se encuentra registrado en SUNAT")));

        var resultado = await Servicio(_servicios).ConsultarTicketAsync(emisor, baja, ct: Ct);
        var otra = await Servicio(_servicios).GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);

        Assert.Equal((EstadoCpe.Rechazado, "2105"), (resultado.Estado, resultado.Codigo));
        var factura = await Historial(_servicios, Factura(123));
        Assert.Equal(EstadoCpe.Aceptado, factura.EstadoActual);
        Assert.True(otra.Exito, Errores(otra));
        Assert.Equal("20123456786-RA-20261022-2", otra.NombreArchivo);
        Assert.Equal(otra.Referencia, (await Historial(_servicios, Factura(123))).Baja);
    }

    [Fact]
    public async Task ConsultarTicketAsync_DesdeOtroProceso_TomaElTicketDelHistorial()
    {
        var (emisor, baja) = await EnviarBajaAsync(_servicios);
        using var otroProceso = new ServiciosPrueba(o => o.RutaBase = _servicios.Almacen.Ruta);
        otroProceso.Sunat.Responder(GetStatus, Status("0", CdrBaja("0", "La Comunicacion de baja RA-20261022-1, ha sido aceptada")));

        var resultado = await Servicio(otroProceso).ConsultarTicketAsync(emisor, baja, ct: Ct);

        Assert.Equal((EstadoCpe.Aceptado, _ticket), (resultado.Estado, resultado.Ticket));
        Assert.Contains($"<ticket>{_ticket}</ticket>", otroProceso.Sunat.Solicitudes[^1].Cuerpo, StringComparison.Ordinal);
        Assert.Equal(EstadoCpe.DeBaja, (await Historial(otroProceso, Factura(123))).EstadoActual);
    }

    [Fact]
    public async Task EnviarAsync_BajaEnProceso_ConsultaElTicketYNoReenvia()
    {
        var (emisor, baja) = await EnviarBajaAsync(_servicios);
        _servicios.Sunat.Responder(GetStatus, Status("0", CdrBaja("0", "La Comunicacion de baja RA-20261022-1, ha sido aceptada")));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, baja, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal((1, 1), (_servicios.Sunat.Llamadas(SendSummary), _servicios.Sunat.Llamadas(GetStatus)));
    }

    [Fact]
    public async Task EnviarAsync_PlazoVencidoDespuesDeGenerar_ErrorLocalSinLlamarASunat()
    {
        var (emisor, baja) = await GenerarBajaAsync(_servicios);
        using var despues = new ServiciosPrueba(o => o.RutaBase = _servicios.Almacen.Ruta, CasosTributarios.Ahora(Fecha.AddDays(8)));

        var resultado = await Servicio(despues).EnviarAsync(emisor, baja, Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, "PCPE-1510"), (resultado.Estado, resultado.Codigo));
        Assert.Equal("Venció el plazo para dar de baja el comprobante: emita una nota de crédito.", resultado.MensajeUsuario);
        Assert.Empty(despues.Sunat.Solicitudes);
    }

    [Fact]
    public async Task FacturaDeBaja_NoSeEnviaNiRegeneraNiAdmiteNotaNiOtraBaja()
    {
        var (emisor, _) = await AceptarBajaAsync(_servicios);
        var llamadas = _servicios.Sunat.Solicitudes.Count;
        var servicio = Servicio(_servicios);

        var envio = await servicio.EnviarAsync(emisor, Factura(123), Ct);
        var regenerada = await servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct);
        var nota = await servicio.GenerarAsync(emisor, CasosNotas.Credito(MotivoNotaCredito.DevolucionTotal, CasosTributarios.Linea(1, 61m)) with
        {
            DocumentoModificado = new DocumentoModificadoCpe { TipoDocumento = TipoDocumentoSunat.Factura, Serie = "F001", Numero = 123, FechaEmision = Fecha },
        }, Ct);
        var otraBaja = await servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);

        Assert.Equal((EstadoCpe.DeBaja, 3), (envio.Estado, envio.Nivel));
        Assert.Equal("El comprobante fue dado de baja en SUNAT: ya no tiene validez.", envio.MensajeUsuario);
        Assert.Equal(llamadas, _servicios.Sunat.Solicitudes.Count);
        Assert.Equal("PCPE-3101", Assert.Single(regenerada.Errores).Codigo);
        Assert.Contains(nota.Errores, e => e.Codigo == "PCPE-1430");
        Assert.Equal("PCPE-1512", Assert.Single(otraBaja.Errores).Codigo);
        Assert.False(EstadoCpe.DeBaja.EsPendiente());
    }

    [Fact]
    public async Task ConsultarCdrAsync_EstadoDeBaja_DevuelveDeBaja()
    {
        using var servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion);
        var emisor = await FacturaAceptadaAsync(servicios, 123);
        servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0003", "El comprobante existe pero está de baja"));

        var resultado = await Servicio(servicios).ConsultarCdrAsync(emisor, Factura(123), Ct);

        Assert.Equal((EstadoCpe.DeBaja, "0003"), (resultado.Estado, resultado.Codigo));
        Assert.Empty(resultado.Errores);
    }

    private static IServicioCpe Servicio(ServiciosPrueba servicios) => servicios.Obtener<IServicioCpe>();

    private static async Task<HistorialCpe> Historial(ServiciosPrueba servicios, ReferenciaCpe referencia) =>
        (await servicios.Obtener<IHistorialCpe>().ObtenerAsync(referencia, Ct))!;

    private static byte[] CdrBaja(string codigo, string descripcion) => Cdr("RA-20261022-1", codigo, descripcion);

    /// <summary>Factura F001-123 aceptada y su RA generada.</summary>
    private static async Task<(EmisorCpe Emisor, ReferenciaCpe Baja)> GenerarBajaAsync(ServiciosPrueba servicios)
    {
        var emisor = await FacturaAceptadaAsync(servicios, 123);
        var resultado = await servicios.Obtener<IServicioCpe>().GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);
        Assert.True(resultado.Exito, Errores(resultado));
        return (emisor, resultado.Referencia);
    }

    private static async Task<(EmisorCpe Emisor, ReferenciaCpe Baja)> EnviarBajaAsync(ServiciosPrueba servicios)
    {
        var (emisor, baja) = await GenerarBajaAsync(servicios);
        servicios.Sunat.Responder(SendSummary, Ticket(_ticket));
        Assert.Equal(EstadoCpe.EnProceso, (await servicios.Obtener<IServicioCpe>().EnviarAsync(emisor, baja, Ct)).Estado);
        return (emisor, baja);
    }

    private static async Task<(EmisorCpe Emisor, ReferenciaCpe Baja)> AceptarBajaAsync(ServiciosPrueba servicios)
    {
        var (emisor, baja) = await EnviarBajaAsync(servicios);
        servicios.Sunat.Responder(GetStatus, Status("0", CdrBaja("0", "La Comunicacion de baja RA-20261022-1, ha sido aceptada")));
        Assert.Equal(EstadoCpe.Aceptado, (await servicios.Obtener<IServicioCpe>().ConsultarTicketAsync(emisor, baja, ct: Ct)).Estado);
        return (emisor, baja);
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
