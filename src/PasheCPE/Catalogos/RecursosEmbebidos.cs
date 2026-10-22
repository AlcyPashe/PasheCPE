using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PasheCPE.Catalogos;

/// <summary>Acceso a los datos versionados embebidos en <c>Recursos\</c> (TRD §3.3).</summary>
internal static class RecursosEmbebidos
{
    private const string _prefijo = "PasheCPE.Recursos.";

    public static Stream Abrir(string nombre) =>
        typeof(RecursosEmbebidos).Assembly.GetManifestResourceStream(_prefijo + nombre)
        ?? throw new InvalidOperationException($"Falta el recurso embebido '{nombre}' en PasheCPE.");

    public static T Leer<T>(string nombre, JsonTypeInfo<T> tipo)
    {
        using var flujo = Abrir(nombre);
        return JsonSerializer.Deserialize(flujo, tipo) ?? throw new InvalidDataException($"El recurso '{nombre}' está vacío.");
    }
}

/// <summary>Contrato de los JSON embebidos.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ArchivoCatalogos))]
[JsonSerializable(typeof(ArchivoErrores))]
[JsonSerializable(typeof(ArchivoErroresLocales))]
[JsonSerializable(typeof(ArchivoTasas))]
[JsonSerializable(typeof(ArchivoPlazos))]
[JsonSerializable(typeof(ArchivoEndpoints))]
[JsonSerializable(typeof(ArchivoMonedas))]
internal sealed partial class DatosJson : JsonSerializerContext;

internal sealed record ArchivoCatalogos(IReadOnlyDictionary<string, DatosCatalogo> Catalogos);

internal sealed record DatosCatalogo(
    string Nombre,
    bool SoloFormato,
    string? Formato,
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<ItemCatalogo> Items);

internal sealed record ItemCatalogo(
    string Codigo,
    string Descripcion,
    IReadOnlyDictionary<string, string>? Extras,
    DateOnly? Desde,
    DateOnly? Hasta);

internal sealed record ArchivoErrores(IReadOnlyList<DatosError> Errores);

/// <summary>Código de retorno de SUNAT: tipo «servicio» (fault), «rechazo» u «observacion».</summary>
internal sealed record DatosError(
    string Codigo,
    string Descripcion,
    string Tipo,
    bool Transitorio,
    IReadOnlyList<string>? Condiciones,
    string? Listados);

internal sealed record ArchivoErroresLocales(IReadOnlyList<DatosErrorLocal> Errores);

internal sealed record DatosErrorLocal(string Codigo, string Descripcion);

internal sealed record ArchivoTasas(
    IReadOnlyList<TramoIgv> Igv,
    IReadOnlyList<TramoTasa> MypeRestaurantesHoteles,
    IReadOnlyList<TramoIcbper> Icbper);

internal sealed record ArchivoPlazos(IReadOnlyList<PlazoEnvio> Plazos);

internal sealed record ArchivoEndpoints(IReadOnlyList<EndpointCpe> Endpoints);

internal sealed record ArchivoMonedas(IReadOnlyDictionary<string, string> Monedas);
