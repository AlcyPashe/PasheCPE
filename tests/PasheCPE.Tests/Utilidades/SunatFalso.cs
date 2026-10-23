using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// SUNAT falso: un <see cref="HttpMessageHandler"/> que responde lo programado por operación (sendBill, getStatusCdr)
/// y guarda cada solicitud. Sin respuesta programada falla la prueba: ninguna prueba llega a la red.
/// </summary>
internal sealed partial class SunatFalso : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Func<CancellationToken, Task<HttpResponseMessage>>>> _respuestas = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<SolicitudRecibida> _solicitudes = new();

    public IReadOnlyList<SolicitudRecibida> Solicitudes => [.. _solicitudes];

    public int Llamadas(string operacion) => _solicitudes.Count(s => s.Operacion == operacion);

    /// <summary>Respuesta HTTP 200 (o la indicada) con el cuerpo dado.</summary>
    public SunatFalso Responder(string operacion, string cuerpo, HttpStatusCode http = HttpStatusCode.OK) =>
        Programar(operacion, _ => Task.FromResult(Respuesta(cuerpo, http)));

    /// <summary>La llamada lanza la excepción (falla de red simulada).</summary>
    public SunatFalso Lanzar(string operacion, Exception excepcion) =>
        Programar(operacion, _ => Task.FromException<HttpResponseMessage>(excepcion));

    /// <summary>La llamada no responde hasta que se cancele (timeout de respuesta).</summary>
    public SunatFalso Colgar(string operacion) =>
        Programar(operacion, async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Inalcanzable.");
        });

    /// <summary>Respuesta que espera a que se complete la tarea (envíos simultáneos).</summary>
    public SunatFalso ResponderTras(string operacion, Task espera, string cuerpo) =>
        Programar(operacion, async _ =>
        {
            await espera;
            return Respuesta(cuerpo, HttpStatusCode.OK);
        });

    public SunatFalso Programar(string operacion, Func<CancellationToken, Task<HttpResponseMessage>> respuesta)
    {
        _respuestas.GetOrAdd(operacion, _ => new()).Enqueue(respuesta);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cuerpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var operacion = Operacion().Match(cuerpo) is { Success: true } m ? m.Groups[1].Value : "?";
        var accion = request.Headers.TryGetValues("SOAPAction", out var valores) ? valores.Single() : null;
        _solicitudes.Enqueue(new SolicitudRecibida(operacion, request.RequestUri!, accion, request.Content?.Headers.ContentType?.ToString(), cuerpo));
        if (!_respuestas.TryGetValue(operacion, out var cola) || !cola.TryDequeue(out var respuesta))
        {
            throw new InvalidOperationException($"SunatFalso: no hay respuesta programada para {operacion}.");
        }

        return await respuesta(cancellationToken);
    }

    private static HttpResponseMessage Respuesta(string cuerpo, HttpStatusCode http) =>
        new(http) { Content = new StringContent(cuerpo, Encoding.UTF8, "text/xml") };

    [GeneratedRegex(@"<ser:(\w+)>")]
    private static partial Regex Operacion();
}

/// <summary>Solicitud recibida por <see cref="SunatFalso"/>.</summary>
internal sealed record SolicitudRecibida(string Operacion, Uri Url, string? SoapAction, string? ContentType, string Cuerpo);

/// <summary>Red falsa del verificador de conectividad: por defecto hay red y todo responde.</summary>
internal sealed class RedFalsa : global::PasheCPE.Conectividad.ISondaRed
{
    private readonly ConcurrentDictionary<string, bool> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private int _sondeos;

    public bool HayInterfaz { get; set; } = true;

    public int Sondeos => _sondeos;

    public void Host(string host, bool responde) => _hosts[host] = responde;

    public bool HayRed() => HayInterfaz;

    public Task<bool> ConectaAsync(string host, int puerto, TimeSpan limite, CancellationToken ct)
    {
        Interlocked.Increment(ref _sondeos);
        return Task.FromResult(_hosts.GetValueOrDefault(host, true));
    }
}
