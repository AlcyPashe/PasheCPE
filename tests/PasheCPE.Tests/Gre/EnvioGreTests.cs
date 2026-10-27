using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasGre;

namespace PasheCPE.Tests.Gre;

/// <summary>
/// Envío de la guía remitente por la API REST (token OAuth2, envío, ticket y CDR) contra el SUNAT falso: sin red.
/// SUNAT no tiene ambiente de pruebas para guías, así que todo se prueba con respuestas grabadas.
/// </summary>
public sealed class EnvioGreTests : IDisposable
{
    private const string _ticket = "a1b2c3d4-0001";

    private readonly ServiciosPrueba _servicios = new(o => o.Ambiente = Ambiente.Produccion);

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EmitirAsync_TokenEnvioTicketYCdr_AceptadaConQrDelCdr()
    {
        var emisor = Emisor(_servicios);
        _servicios.Sunat.Responder(Token, TokenOk())
            .Responder(Envio, Ticket(_ticket))
            .Responder(Consulta, Estado("98"))
            .Responder(Consulta, Estado("0", CdrDorado()));

        var emision = await ConReloj(_servicios, Servicio(_servicios).EmitirAsync(emisor, CasosGre.Dorada(), Ct));

        var envio = emision.Envio;
        Assert.True(emision.Generacion.Exito, string.Join("; ", emision.Generacion.Errores.Select(e => e.Codigo)));
        Assert.Equal((EstadoCpe.AceptadoConObservaciones, _ticket), (envio.Estado, envio.Ticket));
        Assert.Equal(["4391", "4382"], envio.Observaciones.Select(o => o.Codigo));
        Assert.StartsWith("https://e-factura.sunat.gob.pe/v1/contribuyente/gre/comprobantes/descargaqr?hashqr=", envio.ContenidoQr, StringComparison.Ordinal);
        Assert.True(File.Exists(envio.Rutas.Qr));
        Assert.True(File.Exists(envio.Rutas.Cdr));
        Assert.Contains("aceptado por SUNAT", envio.MensajeUsuario, StringComparison.Ordinal);
        Assert.Equal(1, _servicios.Sunat.Llamadas(Token));

        var token = _servicios.Sunat.Solicitudes.Single(s => s.Operacion == Token);
        Assert.Equal("https://api-seguridad.sunat.gob.pe/v1/clientessol/id-cliente/oauth2/token/", token.Url.ToString());
        Assert.Contains("grant_type=password", token.Cuerpo, StringComparison.Ordinal);
        Assert.Contains("username=20123456786MODDATOS", token.Cuerpo, StringComparison.Ordinal);
        Assert.Contains("scope=https%3A%2F%2Fapi-cpe.sunat.gob.pe", token.Cuerpo, StringComparison.Ordinal);

        var solicitud = _servicios.Sunat.Solicitudes.Single(s => s.Operacion == Envio);
        Assert.Equal("https://api-cpe.sunat.gob.pe/v1/contribuyente/gem/comprobantes/20123456786-09-T001-31093", solicitud.Url.ToString());
        Assert.Equal("Bearer tok-1", solicitud.Autorizacion);
        using var cuerpo = JsonDocument.Parse(solicitud.Cuerpo);
        var archivo = cuerpo.RootElement.GetProperty("archivo");
        var zip = await File.ReadAllBytesAsync(emision.Generacion.Rutas.Zip!, Ct);
        Assert.Equal("20123456786-09-T001-31093.zip", archivo.GetProperty("nomArchivo").GetString());
        Assert.Equal(Convert.ToBase64String(zip), archivo.GetProperty("arcGreZip").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(zip)), archivo.GetProperty("hashZip").GetString());
        Assert.All(_servicios.Sunat.Solicitudes.Where(s => s.Operacion == Consulta), s =>
            Assert.Equal($"https://api-cpe.sunat.gob.pe/v1/contribuyente/gem/comprobantes/envios/{_ticket}", s.Url.ToString()));
    }

    [Fact]
    public async Task EnviarAsync_DosGuiasEnLaMismaHora_UnSoloToken_YVencidoSePideOtro()
    {
        var emisor = Emisor(_servicios);
        var primera = await GenerarAsync(_servicios, CasosGre.Dorada());
        var segunda = await GenerarAsync(_servicios, CasosGre.Dorada() with { Numero = 31094 });
        var tercera = await GenerarAsync(_servicios, CasosGre.Dorada() with { Numero = 31095 });
        _servicios.Sunat.Responder(Token, TokenOk("tok-1")).Responder(Token, TokenOk("tok-2"))
            .Responder(Envio, Ticket("t1")).Responder(Envio, Ticket("t2")).Responder(Envio, Ticket("t3"));

        await Servicio(_servicios).EnviarAsync(emisor, primera, Ct);
        _servicios.Reloj.Advance(TimeSpan.FromMinutes(50));
        await Servicio(_servicios).EnviarAsync(emisor, segunda, Ct);
        _servicios.Reloj.Advance(TimeSpan.FromMinutes(10));
        await Servicio(_servicios).EnviarAsync(emisor, tercera, Ct);

        Assert.Equal(2, _servicios.Sunat.Llamadas(Token));
        Assert.Equal(["Bearer tok-1", "Bearer tok-1", "Bearer tok-2"], _servicios.Sunat.Solicitudes.Where(s => s.Operacion == Envio).Select(s => s.Autorizacion));
    }

    [Fact]
    public async Task EnviarAsync_EnviosSimultaneos_UnSoloPedidoDeToken()
    {
        var emisor = Emisor(_servicios);
        var primera = await GenerarAsync(_servicios, CasosGre.Dorada());
        var segunda = await GenerarAsync(_servicios, CasosGre.Dorada() with { Numero = 31094 });
        var liberar = new TaskCompletionSource();
        _servicios.Sunat.ResponderTras(Token, liberar.Task, TokenOk()).Responder(Envio, Ticket("t1")).Responder(Envio, Ticket("t2"));

        var envios = Task.WhenAll(Servicio(_servicios).EnviarAsync(emisor, primera, Ct), Servicio(_servicios).EnviarAsync(emisor, segunda, Ct));
        await Task.Delay(50, Ct);
        liberar.SetResult();
        var resultados = await envios;

        Assert.All(resultados, r => Assert.Equal(EstadoCpe.EnProceso, r.Estado));
        Assert.Equal(1, _servicios.Sunat.Llamadas(Token));
    }

    [Fact]
    public async Task EnviarAsync_401_RenuevaElTokenYRepiteUnaVez()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk("tok-1")).Responder(Token, TokenOk("tok-2"))
            .Responder(Envio, Error("401", "Unauthorized - Fallo en la autenticación del Cliente"), HttpStatusCode.Unauthorized)
            .Responder(Envio, Ticket(_ticket));

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        Assert.Equal((EstadoCpe.EnProceso, _ticket), (resultado.Estado, resultado.Ticket));
        Assert.Equal(["Bearer tok-1", "Bearer tok-2"], _servicios.Sunat.Solicitudes.Where(s => s.Operacion == Envio).Select(s => s.Autorizacion));
    }

    [Fact]
    public async Task EnviarAsync_401Persistente_ErrorDeServicioSinMasIntentos()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk("tok-1")).Responder(Token, TokenOk("tok-2"))
            .Responder(Envio, Error("401", "Unauthorized"), HttpStatusCode.Unauthorized)
            .Responder(Envio, Error("401", "Unauthorized"), HttpStatusCode.Unauthorized);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        Assert.Equal(EstadoCpe.ErrorServicio, resultado.Estado);
        Assert.Equal(2, _servicios.Sunat.Llamadas(Envio));
    }

    [Fact]
    public async Task EnviarAsync_422_ErrorDeServicioConLosErroresFuncionales()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk())
            .Responder(Envio, Error("422", "Unprocessable Entity", ("1034", "El RUC del nombre del archivo no corresponde")), HttpStatusCode.UnprocessableEntity);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        Assert.Equal((EstadoCpe.ErrorServicio, "1034"), (resultado.Estado, resultado.Codigo));
        Assert.Equal("El RUC del nombre del archivo no corresponde", Assert.Single(resultado.Errores).Descripcion);
    }

    [Fact]
    public async Task EnviarAsync_CredencialesRechazadas_ErrorDeConfiguracionSinEnviar()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, Error("401", "Credenciales inválidas"), HttpStatusCode.Unauthorized);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        Assert.Equal((EstadoCpe.ErrorServicio, "PCPE-4603"), (resultado.Estado, resultado.Codigo));
        Assert.Equal((1, 0), (_servicios.Sunat.Llamadas(Token), _servicios.Sunat.Llamadas(Envio)));
    }

    [Fact]
    public async Task EnviarAsync_503_ServicioNoDisponibleReintentable()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk()).Responder(Envio, Error("503", "Service Unavailable"), HttpStatusCode.ServiceUnavailable);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        Assert.Equal((EstadoCpe.ServicioNoDisponible, true), (resultado.Estado, resultado.EsReintentable));
        Assert.Equal(1, _servicios.Sunat.Llamadas(Envio));
    }

    [Fact]
    public async Task EnviarAsync_ConexionCortada_RespuestaInciertaYLuegoReenviaElMismoZip()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk())
            .Lanzar(Envio, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"))
            .Responder(Envio, Ticket(_ticket));

        var incierta = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);
        var reenvio = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        Assert.Equal((EstadoCpe.RespuestaIncierta, true), (incierta.Estado, incierta.RequiereConsultaEstado));
        Assert.Equal(EstadoCpe.EnProceso, reenvio.Estado);
        var envios = _servicios.Sunat.Solicitudes.Where(s => s.Operacion == Envio).ToList();
        Assert.Equal(envios[0].Cuerpo, envios[1].Cuerpo);
    }

    [Fact]
    public async Task EnviarAsync_ConTicketEnElHistorial_ConsultaEnVezDeReenviar()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk()).Responder(Envio, Ticket(_ticket)).Responder(Consulta, Estado("0", CdrDorado()));
        await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        var resultado = await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        Assert.Equal(EstadoCpe.AceptadoConObservaciones, resultado.Estado);
        Assert.Equal((1, 1), (_servicios.Sunat.Llamadas(Envio), _servicios.Sunat.Llamadas(Consulta)));
    }

    [Fact]
    public async Task ConsultarTicketAsync_99ConError_RechazadaConElCodigo()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk()).Responder(Envio, Ticket(_ticket))
            .Responder(Consulta, Estado("99", numError: 2335, desError: "El documento electrónico ingresado ha sido alterado"));
        await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);

        var resultado = await Servicio(_servicios).ConsultarTicketAsync(emisor, guia, ct: Ct);

        Assert.Equal((EstadoCpe.Rechazado, "2335", _ticket), (resultado.Estado, resultado.Codigo, resultado.Ticket));
        Assert.Null(resultado.Rutas.Qr);
    }

    [Fact]
    public async Task ConsultarTicketAsync_DesdeOtroProceso_TomaElTicketDelHistorial()
    {
        var (emisor, guia) = (Emisor(_servicios), await GenerarAsync(_servicios, CasosGre.Dorada()));
        _servicios.Sunat.Responder(Token, TokenOk()).Responder(Envio, Ticket(_ticket));
        await Servicio(_servicios).EnviarAsync(emisor, guia, Ct);
        using var otro = new ServiciosPrueba(o =>
        {
            o.Ambiente = Ambiente.Produccion;
            o.RutaBase = _servicios.Almacen.Ruta;
        });
        otro.Sunat.Responder(Token, TokenOk()).Responder(Consulta, Estado("98"));

        var resultado = await Servicio(otro).ConsultarTicketAsync(emisor, guia, ct: Ct);

        Assert.Equal((EstadoCpe.EnProceso, _ticket), (resultado.Estado, resultado.Ticket));
        Assert.EndsWith($"/envios/{_ticket}", otro.Sunat.Solicitudes[^1].Url.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarAsync_AmbienteBeta_NoPideTokenNiEnvia()
    {
        using var beta = new ServiciosPrueba();
        var guia = await GenerarAsync(beta, CasosGre.Dorada());

        var resultado = await Servicio(beta).EnviarAsync(Emisor(beta), guia, Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, "PCPE-4602"), (resultado.Estado, resultado.Codigo));
        Assert.Empty(beta.Sunat.Solicitudes);
    }

    [Fact]
    public async Task EnviarAsync_AmbienteBetaConPermiso_EnviaAProduccion()
    {
        using var beta = new ServiciosPrueba(o => o.Gre.PermitirEnvioConAmbienteBeta = true);
        var guia = await GenerarAsync(beta, CasosGre.Dorada());
        beta.Sunat.Responder(Token, TokenOk()).Responder(Envio, Ticket(_ticket));

        var resultado = await Servicio(beta).EnviarAsync(Emisor(beta), guia, Ct);

        Assert.Equal(EstadoCpe.EnProceso, resultado.Estado);
        Assert.StartsWith("https://api-cpe.sunat.gob.pe/", beta.Sunat.Solicitudes[^1].Url.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnviarAsync_SinCredencialesGre_ErrorLocal()
    {
        var guia = await GenerarAsync(_servicios, CasosGre.Dorada());

        var resultado = await Servicio(_servicios).EnviarAsync(Emisor(_servicios) with { CredencialesGre = null }, guia, Ct);

        Assert.Equal((EstadoCpe.ErrorValidacionLocal, "PCPE-4601"), (resultado.Estado, resultado.Codigo));
        Assert.Empty(_servicios.Sunat.Solicitudes);
    }

    [Fact]
    public async Task EnviarAsync_EmisorPorOse_LaGuiaVaASunat()
    {
        var guia = await GenerarAsync(_servicios, CasosGre.Dorada());
        _servicios.Sunat.Responder(Token, TokenOk()).Responder(Envio, Ticket(_ticket));

        var resultado = await Servicio(_servicios).EnviarAsync(Emisor(_servicios) with { Canal = CanalEnvio.Ose("efact") }, guia, Ct);

        Assert.Equal(EstadoCpe.EnProceso, resultado.Estado);
        Assert.Equal(CanalEnvio.ClaveSunat, resultado.Historial[^1].Canal);
        Assert.Contains("SUNAT", resultado.MensajeUsuario, StringComparison.Ordinal);
    }

    private static IServicioGre Servicio(ServiciosPrueba servicios) => servicios.Obtener<IServicioGre>();

    private static EmisorCpe Emisor(ServiciosPrueba servicios) => CertificadoPrueba.Emisor(servicios.Almacen);

    private static async Task<ReferenciaCpe> GenerarAsync(ServiciosPrueba servicios, GuiaRemisionCpe guia)
    {
        var generacion = await Servicio(servicios).GenerarAsync(Emisor(servicios), guia, Ct);
        Assert.True(generacion.Exito, string.Join("; ", generacion.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
        return generacion.Referencia;
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
