using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Logging;
using PasheCPE.Tests.Gre;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;
using Api = PasheCPE.Tests.Utilidades.RespuestasGre;

namespace PasheCPE.Tests.Diagnostico;

/// <summary>
/// TRD §21 y §27.5: ninguna credencial en los logs (todas las categorías y niveles, Trace y Debug incluidos, con los
/// sobres SOAP y el JSON de la GRE completos), en el historial ni en ningún archivo del almacén fuera de CERT. Recorre
/// SUNAT (aceptado, respuesta incierta y consulta del CDR, resumen con ticket), OSE y GRE (token, 401 con renovación,
/// envío y ticket).
/// </summary>
public sealed class AuditoriaCredencialesTests : IDisposable
{
    private const string TokenGre = "tok-secreto-de-la-api";
    private const string TokenRenovado = "tok-secreto-renovado";

    private readonly RegistroCapturado _registro = new();
    private readonly ServiciosPrueba _servicios;

    public AuditoriaCredencialesTests()
    {
        _servicios = new ServiciosPrueba(o => o.Ambiente = Ambiente.Produccion, registrar: _registro.Registrar);
    }

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SunatOseYGre_NingunaCredencialEnLogsNiEnElAlmacen()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var ose = emisor with { Canal = CanalEnvio.Ose("efact"), CredencialesSoap = new CredencialesSoap(emisor.Ruc, "clave-ose-secreta") };
        var cpe = _servicios.Obtener<IServicioCpe>();
        var gre = _servicios.Obtener<IServicioGre>();

        // SUNAT: aceptada; respuesta incierta → getStatusCdr; resumen con ticket.
        _servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));
        Assert.Equal(EstadoCpe.Aceptado, (await cpe.EmitirAsync(emisor, Ejemplos.Factura() with { Numero = 1 }, Ct)).Envio.Estado);
        _servicios.Sunat.Lanzar(SendBill, new HttpRequestException(HttpRequestError.ResponseEnded, "cortada"));
        _servicios.Sunat.Responder(GetStatusCdr, StatusCdr("0001", "La constancia existe", CdrDoradoAceptado()));
        var incierta = await cpe.EmitirAsync(emisor, Ejemplos.Factura() with { Numero = 2 }, Ct);
        Assert.Equal(EstadoCpe.RespuestaIncierta, incierta.Envio.Estado);
        Assert.Equal(EstadoCpe.Aceptado, (await cpe.EnviarAsync(emisor, incierta.Generacion.Referencia, Ct)).Estado);
        var resumen = Assert.Single(await cpe.GenerarResumenAsync(emisor, new ResumenDiarioCpe { FechaReferencia = new DateOnly(2026, 10, 22), Lineas = [LineaResumen()] }, Ct));
        _servicios.Sunat.Responder(SendSummary, Ticket("1700000000001")).Responder(GetStatus, Status("0", CdrDoradoResumen()));
        Assert.Equal(EstadoCpe.EnProceso, (await cpe.EnviarAsync(emisor, resumen.Referencia, Ct)).Estado);
        Assert.Equal(EstadoCpe.Aceptado, (await cpe.ConsultarTicketAsync(emisor, resumen.Referencia, ct: Ct)).Estado);

        // OSE.
        _servicios.Sunat.Responder(SendBill, ConCdr(CdrDoradoAceptado()));
        Assert.Equal(EstadoCpe.Aceptado, (await cpe.EmitirAsync(ose, Ejemplos.Factura() with { Serie = "F002", Numero = 1 }, Ct)).Envio.Estado);

        // GRE: el primer envío recibe 401, se renueva el token y se repite; luego el ticket.
        var guia = await gre.GenerarAsync(emisor, CasosGre.Dorada(), Ct);
        Assert.True(guia.Exito);
        _servicios.Sunat.Responder(Api.Token, Api.TokenOk(TokenGre)).Responder(Api.Token, Api.TokenOk(TokenRenovado))
            .Responder(Api.Envio, Api.Error("401", "Unauthorized - Fallo en la autenticación del Cliente"), HttpStatusCode.Unauthorized)
            .Responder(Api.Envio, Api.Ticket("a1b2c3d4-0001"))
            .Responder(Api.Consulta, Api.Estado("0", Api.CdrDorado()));
        Assert.Equal(EstadoCpe.EnProceso, (await gre.EnviarAsync(emisor, guia.Referencia, Ct)).Estado);
        Assert.Equal(EstadoCpe.AceptadoConObservaciones, (await gre.ConsultarTicketAsync(emisor, guia.Referencia, ct: Ct)).Estado);

        string[] secretos =
        [
            emisor.CredencialesSoap!.Clave,
            ose.CredencialesSoap!.Clave,
            emisor.CredencialesGre!.ClientSecret,
            emisor.CredencialesGre.ClaveSol,
            emisor.Certificado.Clave!,
            TokenGre,
            TokenRenovado,
        ];

        // Los sobres y el JSON sí llegaron al log de Debug (la auditoría no es vacía), enmascarados.
        var eventos = _registro.Eventos;
        Assert.Contains(eventos, e => e.Nivel == LogLevel.Debug && e.Texto.Contains("<wsse:Password>***</wsse:Password>", StringComparison.Ordinal));
        Assert.Contains(eventos, e => e.Nivel == LogLevel.Debug && e.Texto.Contains("arcGreZip", StringComparison.Ordinal));
        Assert.Contains(eventos, e => e.Nivel == LogLevel.Debug && e.Texto.Contains("\"access_token\":\"***\"", StringComparison.Ordinal));
        foreach (var secreto in secretos)
        {
            Assert.DoesNotContain(eventos, e => e.Texto.Contains(secreto, StringComparison.Ordinal));
        }

        // Ningún archivo del almacén, salvo el .pfx de CERT, contiene una credencial (historial, XML, ZIP, CDR, QR).
        var archivos = Directory.GetFiles(_servicios.Almacen.Ruta, "*", SearchOption.AllDirectories)
            .Where(a => !a.StartsWith(Path.Combine(_servicios.Almacen.Ruta, "CERT"), StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Contains(archivos, a => a.Contains($"{Path.DirectorySeparatorChar}LOG{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        foreach (var archivo in archivos)
        {
            var contenido = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(archivo, Ct));
            Assert.All(secretos, s => Assert.DoesNotContain(s, contenido, StringComparison.Ordinal));
        }
    }

    private static LineaResumenCpe LineaResumen() => new()
    {
        TipoDocumento = TipoDocumentoSunat.BoletaDeVenta,
        Serie = "B001",
        Numero = 1,
        Estado = EstadoItemResumen.Adicionar,
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "CLIENTE DE PRUEBA" },
        TotalGravado = 100m,
        Igv = 18m,
        ImporteTotal = 118m,
    };
}
