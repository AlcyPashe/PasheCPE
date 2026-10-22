using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PasheCPE.GeneradorRecursos;

/// <summary>Normalización de los textos del Excel para que la salida sea estable.</summary>
internal static partial class Texto
{
    /// <summary>Texto en una sola línea, con espacios simples.</summary>
    public static string UnaLinea(string? valor) =>
        valor is null ? string.Empty : Espacios().Replace(valor.Replace(' ', ' '), " ").Trim();

    /// <summary>Texto que conserva los saltos de línea, sin espacios repetidos ni líneas vacías.</summary>
    public static string Multilinea(string? valor)
    {
        if (valor is null)
        {
            return string.Empty;
        }

        var lineas = valor.Replace(' ', ' ').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n')
            .Select(l => EspaciosEnLinea().Replace(l, " ").Trim())
            .Where(l => l.Length > 0);
        return string.Join('\n', lineas);
    }

    /// <summary>Clave de comparación: una línea, sin tildes y en mayúsculas.</summary>
    public static string Clave(string? valor) => SinTildes(UnaLinea(valor)).ToUpperInvariant();

    public static string SinTildes(string valor)
    {
        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(caracter);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Código de 1 a 4 dígitos completado con ceros a la izquierda (0100, 2075…); null si no es numérico.</summary>
    public static string? CodigoRetorno(string? valor)
    {
        var limpio = UnaLinea(valor);
        return CodigoNumerico().IsMatch(limpio) ? limpio.PadLeft(4, '0') : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex EspaciosEnLinea();

    [GeneratedRegex(@"^\d{1,4}$")]
    private static partial Regex CodigoNumerico();
}
