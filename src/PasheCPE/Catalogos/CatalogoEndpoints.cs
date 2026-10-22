using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Diagnostico;

namespace PasheCPE.Catalogos;

/// <summary>
/// Endpoints por canal, ambiente y servicio: primero los de <see cref="OpcionesPasheCpe.Endpoints"/> y luego los de
/// <c>endpoints.json</c>. Un endpoint no verificado se advierte en el log la primera vez que se usa.
/// </summary>
internal sealed class CatalogoEndpoints
{
    private readonly EndpointCpe[] _endpoints;
    private readonly ILogger<CatalogoEndpoints> _registro;
    private readonly ConcurrentDictionary<string, bool> _advertidos = new(StringComparer.Ordinal);

    public CatalogoEndpoints(IOptions<OpcionesPasheCpe> opciones, ILogger<CatalogoEndpoints> registro)
    {
        _registro = registro;
        _endpoints = [.. opciones.Value.Endpoints, .. RecursosEmbebidos.Leer("endpoints.json", DatosJson.Default.ArchivoEndpoints).Endpoints];
    }

    public EndpointCpe? Obtener(CanalEnvio canal, Ambiente ambiente, ServicioSunat servicio) =>
        Obtener(canal.Clave, ambiente, servicio);

    public EndpointCpe? Obtener(string canal, Ambiente ambiente, ServicioSunat servicio)
    {
        var endpoint = Array.Find(_endpoints, e =>
            string.Equals(e.Canal, canal, StringComparison.OrdinalIgnoreCase) && e.Ambiente == ambiente && e.Servicio == servicio);
        if (endpoint is { Verificado: false } && _advertidos.TryAdd(endpoint.Url, true))
        {
            RegistroEventos.EndpointNoVerificado(_registro, endpoint.Url, endpoint.Canal, endpoint.Ambiente, endpoint.Servicio);
        }

        return endpoint;
    }
}
