using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PasheCPE.Diagnostico;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Transporte.Gre;

/// <summary>Token GRE vigente o la última respuesta del servicio de tokens que impidió obtenerlo.</summary>
internal sealed record ResultadoTokenGre(string? Token, RespuestaSoap? Falla);

/// <summary>
/// Token OAuth2 de la API de guías (TRD §17, manual GRE §1.2): dura lo que indica <c>expires_in</c> (hoy 1 hora) y sirve
/// para todos los envíos y consultas de ese periodo. Se guarda en memoria por <c>client_id</c> y RUC + usuario SOL hasta
/// <c>expires_in − 60 s</c>; vencido, el siguiente envío pide otro. Un solo pedido a la vez por credencial: los envíos
/// simultáneos esperan y reutilizan el mismo token. Ante fallas de red o HTTP 5xx se reintenta hasta 3 veces; las
/// credenciales rechazadas no se reintentan. Nunca lanza.
/// </summary>
internal sealed class ProveedorTokenGre(ClienteGre cliente, EsperasReintento esperas, TimeProvider tiempo, ILogger<ProveedorTokenGre> registro)
{
    private static readonly TimeSpan _margen = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Vence)> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _bloqueos = new(StringComparer.Ordinal);

    /// <summary>
    /// Token vigente de la credencial; si no hay uno, o si <paramref name="descartar"/> es el que se tenía (lo rechazó la
    /// API con 401), pide otro.
    /// </summary>
    public async Task<ResultadoTokenGre> ObtenerAsync(EndpointCpe endpointToken, string ruc, CredencialesGre credenciales, string? descartar, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credenciales);
        var usuario = ruc + credenciales.UsuarioSol;
        var clave = $"{credenciales.ClientId}|{usuario}";
        if (Vigente(clave, descartar) is { } vigente)
        {
            return new ResultadoTokenGre(vigente, null);
        }

        var bloqueo = _bloqueos.GetOrAdd(clave, _ => new SemaphoreSlim(1, 1));
        await bloqueo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Otro envío pudo obtenerlo mientras se esperaba el bloqueo.
            if (Vigente(clave, descartar) is { } obtenido)
            {
                return new ResultadoTokenGre(obtenido, null);
            }

            _tokens.TryRemove(clave, out _);
            for (var intento = 0; ; intento++)
            {
                var respuesta = await cliente.TokenAsync(endpointToken, ruc, credenciales, ct).ConfigureAwait(false);
                if (respuesta.Http is >= 200 and < 300 && ClienteGre.LeerToken(respuesta.Cuerpo) is { } token)
                {
                    var vence = tiempo.GetUtcNow().AddSeconds(token.ExpiraEnSegundos) - _margen;
                    _tokens[clave] = (token.AccessToken, vence);
                    RegistroEventos.TokenGreObtenido(registro, usuario, vence);
                    return new ResultadoTokenGre(token.AccessToken, null);
                }

                var transitoria = respuesta.Falla != FallaRed.Ninguna || respuesta.Http is >= 500;
                if (!transitoria || intento >= esperas.Esperas.Count)
                {
                    return new ResultadoTokenGre(null, respuesta);
                }

                await Task.Delay(esperas.Esperas[intento], tiempo, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            bloqueo.Release();
        }
    }

    private string? Vigente(string clave, string? descartar) =>
        _tokens.TryGetValue(clave, out var actual) && actual.Vence > tiempo.GetUtcNow() && actual.Token != descartar ? actual.Token : null;
}
