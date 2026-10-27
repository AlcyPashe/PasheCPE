using System.Net.Http;
using System.Text.Json;
using PasheCPE.Catalogos;
using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Resumen;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Servicios;

/// <summary>
/// Envío por OSE (Fase 9) con eFact embebido en producción: usuario tal cual (en eFact, el RUC del emisor),
/// getStatusCdr en el mismo billService con el CDR en <c>document</c> y mensajes que nombran al OSE. Sin red.
/// </summary>
public sealed class EnvioOseTests : IDisposable
{
    private const string _urlEfact = "https://ose.efact.pe/ol-ti-itcpe/billService";

    private readonly ServiciosPrueba _servicios = new(o => o.Ambiente = Ambiente.Produccion);

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnviarAsync_FacturaPorEfact_UsuarioSinPrefijoYMensajeDelOse()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal("Comprobante aceptado por el OSE.", resultado.MensajeUsuario);
        Assert.True(File.Exists(resultado.Rutas.Cdr));
        Assert.Equal(("OSE:efact", _urlEfact), (resultado.Historial[^1].Canal, resultado.Historial[^1].Endpoint));
        var solicitud = Assert.Single(_servicios.Sunat.Solicitudes);
        Assert.Equal(_urlEfact, solicitud.Url.ToString());
        Assert.Equal("\"urn:sendBill\"", solicitud.SoapAction);
        Assert.Contains("<wsse:Username>20123456786</wsse:Username>", solicitud.Cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarAsync_TrasRespuestaInciertaConsultaEnEfactYNoReenvia()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"));
        _servicios.Sunat.Responder(GetStatusCdr, DocumentoEfact(CdrDoradoAceptado()));
        Assert.Equal(EstadoCpe.RespuestaIncierta, (await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct)).Estado);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal((1, 1), (_servicios.Sunat.Llamadas(SendBill), _servicios.Sunat.Llamadas(GetStatusCdr)));
        Assert.Equal(_urlEfact, _servicios.Sunat.Solicitudes[^1].Url.ToString());
        Assert.Equal("\"urn:getStatusCdr\"", _servicios.Sunat.Solicitudes[^1].SoapAction);
        Assert.True(File.Exists(resultado.Rutas.Cdr));
    }

    /// <summary>
    /// [verificar] El fault de eFact cuando no tiene el documento aún no se conoce: no se toma como «no existe», así que
    /// no se reenvía (principio 4) y el historial conserva el estado.
    /// </summary>
    [Fact]
    public async Task EnviarAsync_TrasRespuestaInciertaYFaultEnLaConsulta_NoReenvia()
    {
        var (emisor, referencia) = await GenerarAsync(_servicios);
        _servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"));
        _servicios.Sunat.Responder(GetStatusCdr, Fault("S:Server", "No se encontró el comprobante"));
        await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, referencia, Ct);

        Assert.NotEqual(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal(1, _servicios.Sunat.Llamadas(SendBill));
        Assert.Equal(EstadoCpe.RespuestaIncierta, (await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(referencia, Ct))!.EstadoActual);
        Assert.DoesNotContain("{", resultado.MensajeUsuario, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarAsync_OseEnBeta_SinEndpointYSinLlamar()
    {
        using var beta = new ServiciosPrueba();
        var (emisor, referencia) = await GenerarAsync(beta);

        var resultado = await Servicio(beta).EnviarAsync(emisor, referencia, Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, "PCPE-4004"), (resultado.Estado, resultado.Codigo));
        Assert.Empty(beta.Sunat.Solicitudes);
        Assert.DoesNotContain("{", resultado.MensajeUsuario, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EsperarTicketAsync_ResumenPorEfact_TicketYCdrConContentMessage()
    {
        var (emisorSunat, resumen) = await EnvioResumenTests.GenerarResumenAsync(_servicios);
        var emisor = Ose(emisorSunat);
        _servicios.Sunat.Responder(SendSummary, Ticket("1729876543210"));
        _servicios.Sunat.Responder(GetStatus, StatusEfact("0", Cdr("RC-20261022-1", "0", "El Resumen RC-20261022-1 ha sido aceptado.")));
        Assert.Equal(EstadoCpe.EnProceso, (await Servicio(_servicios).EnviarAsync(emisor, resumen, Ct)).Estado);

        var resultado = await Servicio(_servicios).ConsultarTicketAsync(emisor, resumen, ct: Ct);

        Assert.Equal(EstadoCpe.Aceptado, resultado.Estado);
        Assert.Equal("Comprobante aceptado por el OSE.", resultado.MensajeUsuario);
        Assert.All(_servicios.Sunat.Solicitudes, s => Assert.Equal(_urlEfact, s.Url.ToString()));
    }

    /// <summary>Ningún mensaje al cajero queda con el marcador del receptor, sea cual sea el canal.</summary>
    [Fact]
    public void MensajesUsuario_ConReceptor_SinMarcadoresYNombraAlReceptor()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(LectorDorado.Raiz(), "src", "PasheCPE", "Recursos", "mensajes-usuario.json")));
        var mensajes = json.RootElement.EnumerateObject().SelectMany(s => s.Value.EnumerateObject()).Select(m => m.Value.GetString()!).ToList();

        foreach (var canal in new[] { CanalEnvio.Sunat, CanalEnvio.Ose("efact") })
        {
            Assert.All(mensajes, m => Assert.DoesNotContain("{", CatalogoRespuestas.ConReceptor(m, canal), StringComparison.Ordinal));
        }

        Assert.Equal("El OSE rechazó el comprobante.", CatalogoRespuestas.ConReceptor("{Receptor} rechazó el comprobante.", CanalEnvio.Ose("efact")));
        Assert.Equal("Aceptado por SUNAT.", CatalogoRespuestas.ConReceptor("Aceptado por {receptor}.", CanalEnvio.Sunat));
    }

    private static IServicioCpe Servicio(ServiciosPrueba servicios) => servicios.Obtener<IServicioCpe>();

    /// <summary>Emisor por eFact: el usuario de envío es el RUC del emisor.</summary>
    private static EmisorCpe Ose(EmisorCpe emisor) => emisor with
    {
        Canal = CanalEnvio.Ose("efact"),
        CredencialesSoap = new CredencialesSoap(emisor.Ruc, "clave-ose"),
    };

    private static async Task<(EmisorCpe Emisor, ReferenciaCpe Referencia)> GenerarAsync(ServiciosPrueba servicios)
    {
        var emisor = Ose(CertificadoPrueba.Emisor(servicios.Almacen));
        var generacion = await servicios.Obtener<IServicioCpe>().GenerarAsync(emisor, Ejemplos.Factura(), Ct);
        Assert.True(generacion.Exito, string.Join("; ", generacion.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
        return (emisor, generacion.Referencia);
    }
}
