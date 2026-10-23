using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Tiempo;

namespace PasheCPE.Conectividad;

/// <summary>Acceso a la red del verificador; en las pruebas se reemplaza por un doble.</summary>
internal interface ISondaRed
{
    /// <summary>true si hay alguna interfaz de red disponible.</summary>
    bool HayRed();

    /// <summary>true si se resuelve el host y se abre una conexión TCP al puerto dentro del límite.</summary>
    Task<bool> ConectaAsync(string host, int puerto, TimeSpan limite, CancellationToken ct);
}

/// <summary>Sonda real: <see cref="NetworkInterface"/> y una conexión TCP (DNS incluido).</summary>
internal sealed class SondaRed : ISondaRed
{
    public bool HayRed() => NetworkInterface.GetIsNetworkAvailable();

    public async Task<bool> ConectaAsync(string host, int puerto, TimeSpan limite, CancellationToken ct)
    {
        using var limiteCt = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limiteCt.CancelAfter(limite);
        using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(host, puerto, limiteCt.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}

/// <summary>
/// Verificador de conectividad (TRD §13): interfaz de red → DNS + TCP 443 al destino (3 s) → si falla, el host de
/// control (<see cref="OpcionesConectividad.HostControl"/>): si responde, el servicio no está disponible; si no, no hay
/// Internet. El resultado se reutiliza por destino durante <see cref="OpcionesConectividad.CacheSegundos"/> y se
/// invalida ante cualquier error de red en un envío.
/// </summary>
internal sealed class VerificadorConectividad(
    ISondaRed sonda,
    CatalogoEndpoints endpoints,
    RelojCpe reloj,
    TimeProvider tiempo,
    IOptions<OpcionesPasheCpe> opciones) : IVerificadorConectividad
{
    private const int _puerto = 443;
    private static readonly TimeSpan _limite = TimeSpan.FromSeconds(3);

    private readonly ConcurrentDictionary<string, (EstadoConectividad Estado, DateTimeOffset Vence)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<EstadoConectividad> VerificarAsync(CanalEnvio canal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(canal);
        var endpoint = endpoints.Obtener(canal, opciones.Value.Ambiente, ServicioSunat.Comprobantes);
        return endpoint is not null && Uri.TryCreate(endpoint.Url, UriKind.Absolute, out var uri)
            ? VerificarHostAsync(uri.Host, ct)
            : Task.FromResult(new EstadoConectividad
            {
                Disponibilidad = Disponibilidad.ServicioNoDisponible,
                Destino = canal.Clave,
                Detalle = $"No hay endpoint de comprobantes para {canal.Clave} en {opciones.Value.Ambiente}.",
                Fecha = reloj.AhoraLima(),
            });
    }

    public async Task<EstadoConectividad> VerificarHostAsync(string host, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var ahora = tiempo.GetUtcNow();
        if (_cache.TryGetValue(host, out var guardado) && guardado.Vence > ahora)
        {
            return guardado.Estado with { DesdeCache = true };
        }

        var estado = await Sondear(host, ct).ConfigureAwait(false);
        _cache[host] = (estado, ahora.AddSeconds(opciones.Value.Conectividad.CacheSegundos));
        return estado;
    }

    /// <summary>Descarta el resultado guardado del destino (tras un error de red en un envío).</summary>
    public void Invalidar(string host) => _cache.TryRemove(host, out _);

    private async Task<EstadoConectividad> Sondear(string host, CancellationToken ct)
    {
        if (!sonda.HayRed())
        {
            return Estado(host, Disponibilidad.SinConexionInternet, "No hay ninguna interfaz de red disponible.");
        }

        if (await sonda.ConectaAsync(host, _puerto, _limite, ct).ConfigureAwait(false))
        {
            return Estado(host, Disponibilidad.Disponible, null);
        }

        var control = opciones.Value.Conectividad.HostControl;
        return await sonda.ConectaAsync(control, _puerto, _limite, ct).ConfigureAwait(false)
            ? Estado(host, Disponibilidad.ServicioNoDisponible, $"{host} no responde; {control} sí: hay Internet.")
            : Estado(host, Disponibilidad.SinConexionInternet, $"Ni {host} ni {control} responden.");
    }

    private EstadoConectividad Estado(string host, Disponibilidad disponibilidad, string? detalle) => new()
    {
        Disponibilidad = disponibilidad,
        Destino = host,
        Detalle = detalle,
        Fecha = reloj.AhoraLima(),
    };
}
