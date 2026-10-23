using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Diagnostico;

namespace PasheCPE.Transporte.Soap;

/// <summary>Qué falló en la red, si algo falló.</summary>
internal enum FallaRed
{
    /// <summary>No falló: hubo respuesta HTTP.</summary>
    Ninguna,

    /// <summary>No se llegó a enviar: DNS, conexión TCP, TLS o timeout de conexión.</summary>
    SinConexion,

    /// <summary>Se envió y no hubo respuesta a tiempo, o la conexión se cortó a mitad: SUNAT pudo haberlo recibido.</summary>
    SinRespuesta,
}

/// <summary>Resultado de una llamada SOAP; nunca hay excepción.</summary>
internal sealed record RespuestaSoap(string Operacion, string Endpoint, int? Http, string? Cuerpo, FallaRed Falla, long DuracionMs, string? Excepcion);

/// <summary>Crea el manejador HTTP principal del cliente SOAP; las pruebas registran uno falso.</summary>
internal sealed class FabricaManejadorHttp(Func<HttpMessageHandler> crear)
{
    public HttpMessageHandler Crear() => crear();

    /// <summary>
    /// <see cref="SocketsHttpHandler"/> cuya conexión (DNS + TCP) tiene su propio límite
    /// (<see cref="OpcionesPasheCpe.TimeoutConexionSegundos"/>): un timeout ahí significa que no se envió nada.
    /// </summary>
    public static FabricaManejadorHttp Sockets(IOptions<OpcionesPasheCpe> opciones) => new(() =>
    {
        var limite = TimeSpan.FromSeconds(opciones.Value.TimeoutConexionSegundos);
        return new SocketsHttpHandler
        {
            ConnectTimeout = limite,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
            ConnectCallback = async (contexto, ct) =>
            {
                using var limiteCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
                limiteCt.CancelAfter(limite);
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(contexto.DnsEndPoint, limiteCt.Token).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    socket.Dispose();
                    throw new SocketException((int)SocketError.TimedOut);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };
    });
}

/// <summary>Esperas entre reintentos de las operaciones idempotentes (getStatusCdr; luego getStatus y token GRE).</summary>
internal sealed class EsperasReintento(IReadOnlyList<TimeSpan> esperas)
{
    public static EsperasReintento PorDefecto { get; } = new([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)]);

    /// <summary>Una espera por reintento: su cantidad es el máximo de reintentos (3).</summary>
    public IReadOnlyList<TimeSpan> Esperas { get; } = esperas;
}

/// <summary>
/// Cliente SOAP 1.1 (TRD §14) sobre un <see cref="HttpClient"/> nombrado. Distingue «no se envió» (DNS, TCP, TLS,
/// timeout de conexión → <see cref="FallaRed.SinConexion"/>) de «se envió y no hubo respuesta» (timeout de respuesta,
/// conexión cortada, cancelación durante la llamada → <see cref="FallaRed.SinRespuesta"/>). Nunca lanza: una
/// cancelación del llamador a mitad de la llamada también es <see cref="FallaRed.SinRespuesta"/>, porque SUNAT pudo
/// haber recibido el documento. No reintenta: eso lo decide quien llama según la operación.
/// </summary>
internal sealed class ClienteSoap(IHttpClientFactory fabrica, IOptions<OpcionesPasheCpe> opciones, ILogger<ClienteSoap> registro)
{
    public const string NombreCliente = "PasheCPE.Soap";

    public async Task<RespuestaSoap> LlamarAsync(string endpoint, string operacion, string? soapAction, string sobre, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        RegistroEventos.SobreSoapEnviado(registro, operacion, endpoint, new SobreEnmascarado(sobre));
        var inicio = Stopwatch.GetTimestamp();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(opciones.Value.TimeoutRespuestaSegundos));
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(sobre, new UTF8Encoding(false), new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" }),
        };
        solicitud.Headers.TryAddWithoutValidation("SOAPAction", $"\"{(soapAction ?? string.Empty).Replace("{operacion}", operacion, StringComparison.Ordinal)}\"");

        try
        {
            using var cliente = fabrica.CreateClient(NombreCliente);
            using var respuesta = await cliente.SendAsync(solicitud, HttpCompletionOption.ResponseContentRead, limite.Token).ConfigureAwait(false);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(limite.Token).ConfigureAwait(false);
            RegistroEventos.SobreSoapRecibido(registro, operacion, (int)respuesta.StatusCode, new SobreEnmascarado(cuerpo));
            return new RespuestaSoap(operacion, endpoint, (int)respuesta.StatusCode, cuerpo, FallaRed.Ninguna, Duracion(inicio), null);
        }
        catch (HttpRequestException ex) when (ex.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError or HttpRequestError.ProxyTunnelError)
        {
            return Falla(operacion, endpoint, FallaRed.SinConexion, inicio, ex);
        }
        catch (HttpRequestException ex)
        {
            return Falla(operacion, endpoint, FallaRed.SinRespuesta, inicio, ex);
        }
        catch (OperationCanceledException ex)
        {
            return Falla(operacion, endpoint, FallaRed.SinRespuesta, inicio, ex);
        }
        catch (IOException ex)
        {
            return Falla(operacion, endpoint, FallaRed.SinRespuesta, inicio, ex);
        }
    }

    private RespuestaSoap Falla(string operacion, string endpoint, FallaRed falla, long inicio, Exception ex)
    {
        var resumen = Resumen(ex);
        RegistroEventos.FallaRedSoap(registro, operacion, endpoint, falla, resumen);
        return new RespuestaSoap(operacion, endpoint, null, null, falla, Duracion(inicio), resumen);
    }

    private static string Resumen(Exception ex)
    {
        var interna = ex.InnerException is null ? string.Empty : $" → {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
        return $"{ex.GetType().Name}: {ex.Message}{interna}";
    }

    private static long Duracion(long inicio) => (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
}
