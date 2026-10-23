using System.Collections.Frozen;
using System.Xml;
using PasheCPE.Catalogos;

namespace PasheCPE.Validacion.Emulacion;

/// <summary>
/// Resuelve las rutas <c>local:///</c> de DataPower que usan las XSL de SUNAT (TRD §9.3) con los recursos embebidos de
/// PasheCPE. Cualquier otra URI se rechaza: nada se resuelve en red ni en disco.
/// </summary>
internal sealed class ResolutorRecursosLocales : XmlResolver
{
    public const string EsquemaLocal = "local";

    private static readonly FrozenDictionary<string, string> _rutas = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["/commons/error/validate_utils.xsl"] = "xsl-utils.validate_utils.xsl",
        ["/commons/error/error_utils.xsl"] = "xsl-utils.error_utils.xsl",
        ["/commons/cpe/datos/dat_fechas.xml"] = "xsl-utils.dat_fechas.xml",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static ResolutorRecursosLocales Instancia { get; } = new();

    /// <summary>Rutas <c>local:///</c> que se pueden resolver.</summary>
    public static IEnumerable<string> Rutas => _rutas.Keys.Select(r => $"{EsquemaLocal}://{r}");

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        ArgumentNullException.ThrowIfNull(absoluteUri);
        if (absoluteUri.Scheme == EsquemaLocal && _rutas.TryGetValue(absoluteUri.AbsolutePath, out var recurso)
            && (ofObjectToReturn is null || ofObjectToReturn == typeof(Stream) || ofObjectToReturn == typeof(object)))
        {
            return RecursosEmbebidos.Abrir(recurso);
        }

        throw new XmlException($"No se permite resolver '{absoluteUri}': las XSL solo usan los recursos local:/// de PasheCPE.");
    }
}
