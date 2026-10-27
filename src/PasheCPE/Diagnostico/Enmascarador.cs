using System.Text.RegularExpressions;

namespace PasheCPE.Diagnostico;

/// <summary>Sobre SOAP para el log: se enmascara solo si el evento se escribe (al formatearlo).</summary>
internal readonly struct SobreEnmascarado(string? sobre)
{
    public override string ToString() => Enmascarador.Sobre(sobre);
}

/// <summary>
/// Enmascara lo que no debe llegar a los logs (TRD §21): la clave del <c>wsse:Password</c> pasa a «***» y el Base64
/// largo (ZIP enviado, CDR recibido) se trunca.
/// </summary>
internal static partial class Enmascarador
{
    private const int _visibles = 40;

    public static string Sobre(string? sobre)
    {
        if (string.IsNullOrEmpty(sobre))
        {
            return string.Empty;
        }

        var sinClave = Clave().Replace(sobre, "${apertura}***${cierre}");
        return Base64().Replace(sinClave, m => m.Groups["valor"].Length <= _visibles
            ? m.Value
            : $"{m.Groups["apertura"].Value}{m.Groups["valor"].Value[.._visibles]}…({m.Groups["valor"].Length} caracteres){m.Groups["cierre"].Value}");
    }

    [GeneratedRegex(@"(?<apertura><(?:\w+:)?Password\b[^>]*>)[^<]*(?<cierre></(?:\w+:)?Password>)", RegexOptions.IgnoreCase)]
    private static partial Regex Clave();

    [GeneratedRegex(@"(?<apertura><(?:\w+:)?(?:contentFile|applicationResponse|content|document|arcGreZip|arcCdr)\b[^>]*>)(?<valor>[A-Za-z0-9+/=\s]+)(?<cierre></(?:\w+:)?(?:contentFile|applicationResponse|content|document|arcGreZip|arcCdr)>)")]
    private static partial Regex Base64();
}
