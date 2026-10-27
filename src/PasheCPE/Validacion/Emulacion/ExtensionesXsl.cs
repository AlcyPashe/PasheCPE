using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using PasheCPE.Catalogos;

namespace PasheCPE.Validacion.Emulacion;

// Los métodos de estos objetos de extensión se llaman desde XPath: XslCompiledTransform distingue mayúsculas y
// minúsculas, así que conservan el nombre de la función (regexp:test, date:seconds, dp:variable…).

/// <summary>
/// Extensión <c>dp:variable</c> de DataPower. Las XSL de SUNAT solo leen el nombre del archivo enviado
/// (RUC-TT-SERIE-NUMERO.xml), del que obtienen el RUC, el tipo, la serie y el número.
/// </summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Nombre de la función XPath.")]
internal sealed class ExtensionesDataPower(string nombreArchivo)
{
    public const string Espacio = "http://www.datapower.com/extensions";
    public const string ClaveNombreArchivo = "var://context/cpe/nombreArchivoEnviado";

    /// <summary>Claves de contexto que se pueden leer.</summary>
    public static IReadOnlyList<string> Claves { get; } = [ClaveNombreArchivo];

    public string variable(string clave) =>
        string.Equals(clave, ClaveNombreArchivo, StringComparison.Ordinal) ? nombreArchivo : throw new ClaveDataPowerDesconocidaException(clave);
}

/// <summary>Una XSL pidió a <c>dp:variable</c> una clave que PasheCPE no emula.</summary>
internal sealed class ClaveDataPowerDesconocidaException(string clave)
    : InvalidOperationException($"La XSL pidió la variable de DataPower '{clave}', que PasheCPE no emula.")
{
    public string Clave { get; } = clave;
}

/// <summary>Funciones EXSLT de fechas (<c>http://exslt.org/dates-and-times</c>) en hora de Lima.</summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Nombre de la función XPath.")]
internal sealed class ExtensionesExsltFechas(DateTimeOffset ahoraLima)
{
    public const string Espacio = "http://exslt.org/dates-and-times";

    private static readonly DateTime _epoca = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Fecha actual con zona: 2026-10-22-05:00.</summary>
    public string date() =>
        ahoraLima.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ahoraLima.ToString("zzz", CultureInfo.InvariantCulture);

    /// <summary>Segundos desde 1970-01-01T00:00:00Z de una fecha xs:date o xs:dateTime; NaN si no es válida.</summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XslCompiledTransform invoca métodos del objeto de extensión.")]
    public double seconds(string fecha)
    {
        var texto = fecha?.Trim() ?? string.Empty;
        string[] formatos = ["yyyy-MM-dd", "yyyy-MM-ddzzz", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-ddTHH:mm:ssZ"];
        if (texto.EndsWith('Z') && texto.Length == 11)
        {
            texto = texto[..^1];
        }

        return DateTimeOffset.TryParseExact(texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var valor)
            ? (valor.UtcDateTime - _epoca).TotalSeconds
            : double.NaN;
    }
}

/// <summary>Funciones EXSLT de expresiones regulares (<c>http://exslt.org/regular-expressions</c>).</summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Nombre de la función XPath.")]
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XslCompiledTransform invoca métodos del objeto de extensión.")]
internal sealed class ExtensionesExsltRegexp
{
    public const string Espacio = "http://exslt.org/regular-expressions";

    private static readonly ConcurrentDictionary<(string Patron, bool IgnorarMayusculas), Regex> _cache = new();

    public bool test(string texto, string patron) => Obtener(patron, string.Empty).IsMatch(texto);

    public bool test(string texto, string patron, string banderas) => Obtener(patron, banderas).IsMatch(texto);

    public XPathNodeIterator match(string texto, string patron) => match(texto, patron, string.Empty);

    /// <summary>
    /// Sin «g»: el texto coincidente y luego cada grupo; con «g»: cada coincidencia. Cada uno en un elemento
    /// <c>match</c>.
    /// </summary>
    public XPathNodeIterator match(string texto, string patron, string banderas)
    {
        var regex = Obtener(patron, banderas);
        var raiz = new XElement("r");
        if (banderas.Contains('g', StringComparison.Ordinal))
        {
            foreach (Match coincidencia in regex.Matches(texto))
            {
                raiz.Add(new XElement("match", coincidencia.Value));
            }
        }
        else if (regex.Match(texto) is { Success: true } coincidencia)
        {
            foreach (Group grupo in coincidencia.Groups)
            {
                raiz.Add(new XElement("match", grupo.Value));
            }
        }

        return new XDocument(raiz).CreateNavigator().Select("/r/match");
    }

    public string replace(string texto, string patron, string banderas, string reemplazo)
    {
        var regex = Obtener(patron, banderas);
        // EXSLT reemplaza el texto literal (sin $1 ni $$).
        var literal = reemplazo.Replace("$", "$$", StringComparison.Ordinal);
        return banderas.Contains('g', StringComparison.Ordinal) ? regex.Replace(texto, literal) : regex.Replace(texto, literal, 1);
    }

    private static Regex Obtener(string patron, string banderas) =>
        _cache.GetOrAdd(
            (patron, banderas.Contains('i', StringComparison.Ordinal)),
            clave => new Regex(clave.Patron, RegexOptions.CultureInvariant | (clave.IgnorarMayusculas ? RegexOptions.IgnoreCase : RegexOptions.None), TimeSpan.FromSeconds(1)));
}

/// <summary>
/// Funciones propias que usan las utilidades de <c>validate_utils.xsl</c> (<c>urn:pashecpe:xsl</c>): catálogos a la
/// fecha de recepción (hoy en Lima, como DataPower al recibir), fecha actual y ruta XPath de un nodo.
/// </summary>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Nombre de la función XPath.")]
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XslCompiledTransform invoca métodos del objeto de extensión.")]
internal sealed class ExtensionesPasheCpe(CatalogosSunat catalogos, DateOnly hoyLima)
{
    public const string Espacio = "urn:pashecpe:xsl";

    private static readonly Lazy<FrozenSet<string>> _monedas = new(() =>
        RecursosEmbebidos.Leer("monedas.json", DatosJson.Default.ArchivoMonedas).Monedas.Keys.ToFrozenSet(StringComparer.Ordinal));

    /// <summary>
    /// Propiedades de catálogo que consultan las XSL (<c>findElementInCatalogProperty</c>) y la columna del Excel de
    /// SUNAT que las respalda. La propiedad de tributo del catálogo N.° 07 indica si la afectación admite ese tributo.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Propiedades { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["05"] = ["name", "UN_ECE_5153"],
        ["07"] = ["igv", "iva", "exp", "gra", "exo", "ina", "oth"],
        ["22"] = ["tasa"],
        ["51"] = ["factura", "boleta"],
        ["61"] = ["gre-r", "gre-t"],
        ["63"] = ["ubigeo"],
        ["64"] = ["ubigeo"],
    };

    private static readonly FrozenDictionary<string, string[]> _tributosAfectacion = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["igv"] = ["1000"],
        ["iva"] = ["1016"],
        ["exp"] = ["9995"],
        ["gra"] = ["9996"],
        ["exo"] = ["9997"],
        ["ina"] = ["9998"],
        ["oth"] = ["9999", "7152"],
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Catálogos con lista propia en PasheCPE en lugar de la validación por formato.</summary>
    public const string CatalogoMonedas = "02";

    public string hoy() => hoyLima.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>true si el código existe y está vigente hoy (o cumple el formato, en los catálogos que SUNAT solo enlaza).</summary>
    public bool enCatalogo(string catalogo, string codigo) =>
        string.Equals(catalogo, CatalogoMonedas, StringComparison.Ordinal)
            ? _monedas.Value.Contains(codigo)
            : catalogos.Validar(catalogo, codigo, hoyLima) is null;

    /// <summary>Valor de la propiedad del código; vacío si el código no existe o no está vigente.</summary>
    public string propiedadCatalogo(string catalogo, string codigo, string propiedad)
    {
        if (!Propiedades.TryGetValue(catalogo, out var conocidas) || !conocidas.Contains(propiedad))
        {
            throw new PropiedadCatalogoDesconocidaException(catalogo, propiedad);
        }

        if (!enCatalogo(catalogo, codigo))
        {
            return string.Empty;
        }

        return (catalogo, propiedad) switch
        {
            ("05", "name") => catalogos.Extra(catalogo, codigo, "Nombre") ?? string.Empty,
            ("05", "UN_ECE_5153") => catalogos.Extra(catalogo, codigo, "Código internacional") ?? string.Empty,
            ("07", _) => Indicador(Codigos(catalogos.Extra(catalogo, codigo, "Codigo de tributo")).Overlaps(_tributosAfectacion[propiedad])),
            ("22", "tasa") => catalogos.Extra(catalogo, codigo, "Porcentaje %") ?? string.Empty,
            ("51", "factura") => Indicador(TiposAsociados(catalogos.Extra(catalogo, codigo, "Tipo de Comprobante asociado")).Contains("factura")),
            ("51", "boleta") => Indicador(TiposAsociados(catalogos.Extra(catalogo, codigo, "Tipo de Comprobante asociado")).Contains("boleta")),
            ("61", "gre-r") => Indicador(GreAplicable(catalogos.Extra(catalogo, codigo, "GRE Aplicable"), "remitente")),
            ("61", "gre-t") => Indicador(GreAplicable(catalogos.Extra(catalogo, codigo, "GRE Aplicable"), "transportista")),
            ("63" or "64", "ubigeo") => catalogos.Extra(catalogo, codigo, "Ubigeo") ?? string.Empty,
            _ => throw new PropiedadCatalogoDesconocidaException(catalogo, propiedad),
        };
    }

    /// <summary>Ruta XPath del primer nodo: /Invoice/cac:InvoiceLine[2]/cbc:ID[1] o …/@currencyID; vacía si no es un nodo.</summary>
    public string ruta(object nodo)
    {
        if (nodo is not XPathNodeIterator iterador)
        {
            return string.Empty;
        }

        var copia = iterador.Clone();
        return copia.MoveNext() && copia.Current is { NodeType: XPathNodeType.Element or XPathNodeType.Attribute } actual
            ? Reglas.DocumentoXml.Ruta(actual)
            : string.Empty;
    }

    private static string Indicador(bool valor) => valor ? "1" : "0";

    /// <summary>«remitente, transportista», «solo remitente» o «solo transportista» (columna «GRE Aplicable» del N.° 61).</summary>
    private static bool GreAplicable(string? texto, string guia) =>
        texto is not null && texto.Contains(guia, StringComparison.OrdinalIgnoreCase);

    /// <summary>«1016 o 9996» → {1016, 9996}.</summary>
    private static HashSet<string> Codigos(string? texto)
    {
        var codigos = new HashSet<string>(StringComparer.Ordinal);
        var actual = new StringBuilder();
        foreach (var caracter in (texto ?? string.Empty) + " ")
        {
            if (char.IsAsciiDigit(caracter))
            {
                actual.Append(caracter);
            }
            else if (actual.Length > 0)
            {
                codigos.Add(actual.ToString());
                actual.Clear();
            }
        }

        return codigos;
    }

    /// <summary>«Factura, Boletas» → {factura, boleta}.</summary>
    private static HashSet<string> TiposAsociados(string? texto) =>
        (texto ?? string.Empty)
            .Split([',', '/', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant().TrimEnd('s'))
            .ToHashSet(StringComparer.Ordinal);
}

/// <summary>Una XSL consultó una propiedad de catálogo que PasheCPE no emula.</summary>
internal sealed class PropiedadCatalogoDesconocidaException(string catalogo, string propiedad)
    : InvalidOperationException($"La XSL consultó la propiedad '{propiedad}' del catálogo N.° {catalogo}, que PasheCPE no emula.");
