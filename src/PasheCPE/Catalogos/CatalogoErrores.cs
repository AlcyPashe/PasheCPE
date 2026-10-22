using System.Collections.Frozen;
using System.Globalization;

namespace PasheCPE.Catalogos;

/// <summary>
/// Códigos de retorno de SUNAT (<c>errores-sunat.json</c>, generado del Excel) y mensajes propios de la validación
/// local (<c>errores-locales.json</c>, códigos PCPE-xxxx).
/// </summary>
internal sealed class CatalogoErrores
{
    private readonly Lazy<FrozenDictionary<string, DatosError>> _sunat = new(() =>
        RecursosEmbebidos.Leer("errores-sunat.json", DatosJson.Default.ArchivoErrores).Errores
            .ToFrozenDictionary(e => e.Codigo, StringComparer.Ordinal));

    private readonly Lazy<FrozenDictionary<string, string>> _locales = new(() =>
        RecursosEmbebidos.Leer("errores-locales.json", DatosJson.Default.ArchivoErroresLocales).Errores
            .ToFrozenDictionary(e => e.Codigo, e => e.Descripcion, StringComparer.Ordinal));

    /// <summary>Busca un código de SUNAT; acepta «111» o «0111».</summary>
    public DatosError? Buscar(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var limpio = codigo.Trim();
        if (limpio.Length < 4 && limpio.All(char.IsAsciiDigit))
        {
            limpio = limpio.PadLeft(4, '0');
        }

        return _sunat.Value.GetValueOrDefault(limpio);
    }

    /// <summary>Error de la validación local con su mensaje de <c>errores-locales.json</c>.</summary>
    public ErrorCpe Local(string codigo, OrigenError origen, string? ruta, string? catalogo, params object?[] argumentos) => new()
    {
        Codigo = codigo,
        Descripcion = Mensaje(codigo, argumentos),
        Ruta = ruta,
        Origen = origen,
        Catalogo = catalogo,
    };

    /// <summary>Advertencia con su mensaje de <c>errores-locales.json</c>.</summary>
    public ObservacionCpe Advertencia(string codigo, params object?[] argumentos) => new()
    {
        Codigo = codigo,
        Descripcion = Mensaje(codigo, argumentos),
    };

    private string Mensaje(string codigo, object?[] argumentos)
    {
        var plantilla = _locales.Value.GetValueOrDefault(codigo)
            ?? throw new InvalidOperationException($"No existe el mensaje local '{codigo}' en errores-locales.json.");
        return string.Format(CultureInfo.InvariantCulture, plantilla, argumentos);
    }
}
