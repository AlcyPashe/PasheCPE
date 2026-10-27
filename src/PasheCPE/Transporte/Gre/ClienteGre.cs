using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Diagnostico;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Transporte.Gre;

/// <summary>Operaciones de la API REST de guías (nombres del historial y del log).</summary>
internal static class OperacionGre
{
    public const string Token = "tokenGre";
    public const string Envio = "envioGre";
    public const string Consulta = "consultaGre";
}

/// <summary>Token OAuth2 de la API de guías.</summary>
internal sealed record TokenGre(string AccessToken, int ExpiraEnSegundos);

/// <summary>Respuesta del envío: el ticket y la fecha de recepción.</summary>
internal sealed record TicketGre(string NumTicket, string? FecRecepcion);

/// <summary>Estado del ticket: codRespuesta (0, 98, 99), error, CDR en Base64 e indicador de CDR generado.</summary>
internal sealed record EstadoGre(string? CodRespuesta, string? NumError, string? DesError, string? ArcCdr, string? IndCdrGenerado);

/// <summary>Error de la API (manual GRE §1.3): cod, msg y, con HTTP 422, los errores funcionales (codError, desError).</summary>
internal sealed record ErrorApiGre(string? Cod, string? Msg, IReadOnlyList<(string Codigo, string Descripcion)> Errores);

/// <summary>
/// Cliente de la API REST de SUNAT para guías (TRD §17, manual «Servicios web – Plataforma nueva GRE»): token OAuth2
/// (POST form), envío (POST JSON con el ZIP en Base64 y su SHA-256) y consulta del ticket (GET). Usa el mismo
/// <see cref="HttpClient"/> nombrado y los mismos límites de tiempo que el cliente SOAP; distingue igual «no se envió»
/// de «se envió y no hubo respuesta» y nunca lanza. Las respuestas se leen con <see cref="JsonDocument"/> de forma
/// tolerante: SUNAT devuelve códigos como texto o como número.
/// </summary>
internal sealed class ClienteGre(IHttpClientFactory fabrica, IOptions<OpcionesPasheCpe> opciones, ILogger<ClienteGre> registro)
{
    private const string _scope = "https://api-cpe.sunat.gob.pe";

    /// <summary>POST del token: grant_type=password, scope, client_id, client_secret, username = RUC + usuario SOL y password.</summary>
    public Task<RespuestaSoap> TokenAsync(string urlPlantilla, string ruc, CredencialesGre credenciales, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credenciales);
        var url = urlPlantilla.Replace("{client_id}", Uri.EscapeDataString(credenciales.ClientId), StringComparison.Ordinal);
        var formulario = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "password",
            ["scope"] = _scope,
            ["client_id"] = credenciales.ClientId,
            ["client_secret"] = credenciales.ClientSecret,
            ["username"] = ruc + credenciales.UsuarioSol,
            ["password"] = credenciales.ClaveSol,
        });
        // El formulario lleva el client_secret y la clave SOL: no se registra.
        return LlamarAsync(HttpMethod.Post, url, OperacionGre.Token, formulario, null, "(credenciales)", ct);
    }

    /// <summary>POST {api}/{nombre} con {"archivo":{"nomArchivo","arcGreZip","hashZip"}}.</summary>
    public Task<RespuestaSoap> EnviarAsync(string urlApi, string token, string nombre, byte[] zip, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(zip);
        var cuerpo = JsonSerializer.Serialize(new
        {
            archivo = new
            {
                nomArchivo = nombre + ".zip",
                arcGreZip = Convert.ToBase64String(zip),
                hashZip = HashZip(zip),
            },
        });
        var contenido = new StringContent(cuerpo, new UTF8Encoding(false), new MediaTypeHeaderValue("application/json"));
        return LlamarAsync(HttpMethod.Post, $"{urlApi.TrimEnd('/')}/{nombre}", OperacionGre.Envio, contenido, token, cuerpo, ct);
    }

    /// <summary>GET {api}/envios/{numTicket}.</summary>
    public Task<RespuestaSoap> ConsultarAsync(string urlApi, string token, string ticket, CancellationToken ct) =>
        LlamarAsync(HttpMethod.Get, $"{urlApi.TrimEnd('/')}/envios/{Uri.EscapeDataString(ticket)}", OperacionGre.Consulta, null, token, string.Empty, ct);

    /// <summary>SHA-256 del ZIP en hexadecimal en minúsculas (TRD §17, [verificar] con el primer envío real).</summary>
    public static string HashZip(byte[] zip) => Convert.ToHexStringLower(SHA256.HashData(zip));

    public static TokenGre? LeerToken(string? cuerpo) => Leer(cuerpo, raiz =>
        Texto(raiz, "access_token") is { Length: > 0 } token
            ? new TokenGre(token, int.TryParse(Texto(raiz, "expires_in"), out var segundos) ? segundos : 0)
            : null);

    public static TicketGre? LeerTicket(string? cuerpo) => Leer(cuerpo, raiz =>
        Texto(raiz, "numTicket") is { Length: > 0 } ticket ? new TicketGre(ticket, Texto(raiz, "fecRecepcion")) : null);

    public static EstadoGre? LeerEstado(string? cuerpo) => Leer(cuerpo, raiz =>
    {
        if (Texto(raiz, "codRespuesta") is not { Length: > 0 } codigo)
        {
            return null;
        }

        var error = raiz.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? e : default;
        return new EstadoGre(
            codigo,
            error.ValueKind == JsonValueKind.Object ? Texto(error, "numError") : null,
            error.ValueKind == JsonValueKind.Object ? Texto(error, "desError") : null,
            Texto(raiz, "arcCdr"),
            Texto(raiz, "indCdrGenerado"));
    });

    public static ErrorApiGre? LeerError(string? cuerpo) => Leer(cuerpo, raiz =>
    {
        var lista = new List<(string, string)>();
        if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Array)
        {
            foreach (var error in errores.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
            {
                lista.Add((Texto(error, "codError") ?? string.Empty, Texto(error, "desError") ?? string.Empty));
            }
        }

        var cod = Texto(raiz, "cod");
        var msg = Texto(raiz, "msg");
        return cod is null && msg is null && lista.Count == 0 ? null : new ErrorApiGre(cod, msg, lista);
    });

    private async Task<RespuestaSoap> LlamarAsync(HttpMethod metodo, string url, string operacion, HttpContent? contenido, string? token, string registroCuerpo, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        RegistroEventos.SolicitudGre(registro, operacion, url, new SobreEnmascarado(registroCuerpo));
        var inicio = Stopwatch.GetTimestamp();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(opciones.Value.TimeoutRespuestaSegundos));
        using var solicitud = new HttpRequestMessage(metodo, url) { Content = contenido };
        solicitud.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (token is not null)
        {
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var cliente = fabrica.CreateClient(ClienteSoap.NombreCliente);
            using var respuesta = await cliente.SendAsync(solicitud, HttpCompletionOption.ResponseContentRead, limite.Token).ConfigureAwait(false);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(limite.Token).ConfigureAwait(false);
            RegistroEventos.RespuestaGre(registro, operacion, (int)respuesta.StatusCode, new SobreEnmascarado(cuerpo));
            return new RespuestaSoap(operacion, url, (int)respuesta.StatusCode, cuerpo, FallaRed.Ninguna, Duracion(inicio), null);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError or HttpRequestError.ProxyTunnelError)
        {
            return Falla(operacion, url, FallaRed.SinConexion, inicio, ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
        {
            return Falla(operacion, url, FallaRed.SinRespuesta, inicio, ex);
        }
    }

    private RespuestaSoap Falla(string operacion, string url, FallaRed falla, long inicio, Exception ex)
    {
        var resumen = ex.InnerException is null ? $"{ex.GetType().Name}: {ex.Message}" : $"{ex.GetType().Name}: {ex.Message} → {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        RegistroEventos.FallaRedSoap(registro, operacion, url, falla, resumen);
        return new RespuestaSoap(operacion, url, null, null, falla, Duracion(inicio), resumen);
    }

    private static T? Leer<T>(string? cuerpo, Func<JsonElement, T?> leer)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            return null;
        }

        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            return documento.RootElement.ValueKind == JsonValueKind.Object ? leer(documento.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Valor de la propiedad como texto, sea cadena, número o booleano; null si no existe o es null.</summary>
    private static string? Texto(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor)
            ? valor.ValueKind switch
            {
                JsonValueKind.String => valor.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => valor.GetRawText(),
                _ => null,
            }
            : null;

    private static long Duracion(long inicio) => (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
}
