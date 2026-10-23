using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PasheCPE.Tests.Dorados;

/// <summary>Diferencia entre el XML esperado (dorado) y el generado.</summary>
internal sealed record Diferencia(string Ruta, string Tipo, string? Esperado, string? Obtenido)
{
    public override string ToString() => $"{Tipo} {Ruta}: esperado «{Esperado}», obtenido «{Obtenido}»";
}

/// <summary>
/// Compara dos XML por estructura y valores, no byte a byte: ignora espacios, orden de atributos y declaraciones de
/// namespace; los números se comparan por valor («8.00» = «8», «20.0000000000» = «20»). Los hermanos repetidos se
/// emparejan por clave cuando la tienen (cbc:Note por @languageLocaleID, cac:TaxSubtotal por el código de tributo) y
/// por posición en los demás casos.
/// </summary>
internal static partial class ComparadorXml
{
    private static readonly Dictionary<string, string> _prefijos = new(StringComparer.Ordinal)
    {
        ["urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"] = "cac",
        ["urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"] = "cbc",
        ["urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2"] = "ext",
        ["http://www.w3.org/2000/09/xmldsig#"] = "ds",
    };

    private static readonly XNamespace _cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace _cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public static IReadOnlyList<Diferencia> Comparar(XElement esperado, XElement obtenido, IReadOnlySet<string> elementosIgnorados)
    {
        var diferencias = new List<Diferencia>();
        Comparar(esperado, obtenido, "/" + esperado.Name.LocalName, elementosIgnorados, diferencias);
        return diferencias;
    }

    private static void Comparar(XElement esperado, XElement obtenido, string ruta, IReadOnlySet<string> ignorados, List<Diferencia> diferencias)
    {
        var atributosEsperados = esperado.Attributes().Where(a => !a.IsNamespaceDeclaration).ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);
        var atributosObtenidos = obtenido.Attributes().Where(a => !a.IsNamespaceDeclaration).ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);
        foreach (var (nombre, valor) in atributosEsperados)
        {
            if (!atributosObtenidos.TryGetValue(nombre, out var otro))
            {
                diferencias.Add(new Diferencia($"{ruta}/@{nombre}", "AtributoFaltante", valor, null));
            }
            else if (!Iguales(valor, otro))
            {
                diferencias.Add(new Diferencia($"{ruta}/@{nombre}", "ValorAtributo", valor, otro));
            }
        }

        foreach (var (nombre, valor) in atributosObtenidos.Where(a => !atributosEsperados.ContainsKey(a.Key)))
        {
            diferencias.Add(new Diferencia($"{ruta}/@{nombre}", "AtributoAdicional", null, valor));
        }

        if (!esperado.HasElements && !obtenido.HasElements)
        {
            if (!Iguales(esperado.Value, obtenido.Value))
            {
                diferencias.Add(new Diferencia(ruta, "Valor", Normalizar(esperado.Value), Normalizar(obtenido.Value)));
            }

            return;
        }

        var nombres = esperado.Elements().Select(e => e.Name).Concat(obtenido.Elements().Select(e => e.Name)).Distinct();
        foreach (var nombre in nombres)
        {
            var etiqueta = Etiqueta(nombre);
            if (ignorados.Contains(etiqueta))
            {
                continue;
            }

            var hijosEsperados = esperado.Elements(nombre).ToList();
            var hijosObtenidos = obtenido.Elements(nombre).ToList();
            var clave = Clave(nombre);
            if (clave is not null)
            {
                var porClave = hijosObtenidos.ToDictionary(clave, StringComparer.Ordinal);
                foreach (var hijo in hijosEsperados)
                {
                    var k = clave(hijo);
                    var rutaHijo = $"{ruta}/{etiqueta}[{k}]";
                    if (porClave.Remove(k, out var otro))
                    {
                        Comparar(hijo, otro, rutaHijo, ignorados, diferencias);
                    }
                    else
                    {
                        diferencias.Add(new Diferencia(rutaHijo, "ElementoFaltante", Normalizar(hijo.Value), null));
                    }
                }

                diferencias.AddRange(porClave.Select(p => new Diferencia($"{ruta}/{etiqueta}[{p.Key}]", "ElementoAdicional", null, Normalizar(p.Value.Value))));
                continue;
            }

            for (var i = 0; i < Math.Max(hijosEsperados.Count, hijosObtenidos.Count); i++)
            {
                var rutaHijo = $"{ruta}/{etiqueta}[{i + 1}]";
                if (i >= hijosObtenidos.Count)
                {
                    diferencias.Add(new Diferencia(rutaHijo, "ElementoFaltante", Normalizar(hijosEsperados[i].Value), null));
                }
                else if (i >= hijosEsperados.Count)
                {
                    diferencias.Add(new Diferencia(rutaHijo, "ElementoAdicional", null, Normalizar(hijosObtenidos[i].Value)));
                }
                else
                {
                    Comparar(hijosEsperados[i], hijosObtenidos[i], rutaHijo, ignorados, diferencias);
                }
            }
        }
    }

    private static Func<XElement, string>? Clave(XName nombre)
    {
        if (nombre == _cbc + "Note")
        {
            return e => (string?)e.Attribute("languageLocaleID") ?? "?";
        }

        if (nombre == _cac + "TaxSubtotal")
        {
            return e => e.Element(_cac + "TaxCategory")?.Element(_cac + "TaxScheme")?.Element(_cbc + "ID")?.Value ?? "?";
        }

        return null;
    }

    private static string Etiqueta(XName nombre) =>
        _prefijos.TryGetValue(nombre.NamespaceName, out var prefijo) ? $"{prefijo}:{nombre.LocalName}" : nombre.LocalName;

    private static bool Iguales(string esperado, string obtenido)
    {
        var a = Normalizar(esperado);
        var b = Normalizar(obtenido);
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return true;
        }

        return decimal.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && decimal.TryParse(b, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            && x == y;
    }

    private static string Normalizar(string texto) => Espacios().Replace(texto, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
