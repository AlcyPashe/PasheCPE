using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace PasheCPE.Validacion.Emulacion;

/// <summary>
/// Correcciones en memoria que hacen compilar con <c>XslCompiledTransform</c> las XSL de SUNAT, escritas para DataPower,
/// que es más permisivo (TRD §9.3). Los archivos originales se embeben sin cambios; las correcciones mantienen la
/// semántica:
/// <list type="bullet">
/// <item><c>concat('…')</c> con un solo argumento → el literal.</item>
/// <item>Variable local declarada otra vez en su ámbito → se renombra la nueva declaración y sus referencias
/// posteriores, que es lo mismo que sombrearla.</item>
/// <item>Variables usadas sin declarar → variables globales de respaldo (<c>xsl-compatibilidad.json</c>).</item>
/// <item><c>xsl:include</c> de las utilidades con la ruta del servidor de SUNAT (las XSL GRE 2.0 incluyen
/// <c>/cpe/data/sunat_archivos/sfs/VALI/commons/error/validate_utils.xsl</c>) → la ruta <c>local:///</c> equivalente.</item>
/// </list>
/// </summary>
internal static partial class AdaptadorXslDataPower
{
    public const string EspacioXslt = "http://www.w3.org/1999/XSL/Transform";

    private static readonly XName _variable = XName.Get("variable", EspacioXslt);
    private static readonly XName _param = XName.Get("param", EspacioXslt);
    private static readonly XName _template = XName.Get("template", EspacioXslt);
    private static readonly XName _include = XName.Get("include", EspacioXslt);
    private const string _utilidades = "/commons/error/validate_utils.xsl";

    /// <summary>Copia adaptada de la XSL y las correcciones aplicadas.</summary>
    public static (XDocument Xsl, CorreccionesXsl Correcciones) Adaptar(XDocument original, IEnumerable<VariableRespaldoXsl> respaldos)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(respaldos);
        var xsl = new XDocument(original);
        var raiz = xsl.Root ?? throw new InvalidOperationException("La XSL no tiene elemento raíz.");

        foreach (var inclusion in raiz.Elements(_include))
        {
            if (inclusion.Attribute("href") is { } href && !href.Value.StartsWith("local:", StringComparison.Ordinal)
                && href.Value.EndsWith(_utilidades, StringComparison.Ordinal))
            {
                href.Value = "local://" + _utilidades;
            }
        }

        var concat = CorregirConcat(raiz);
        var redeclaradas = new List<string>();
        foreach (var plantilla in raiz.Elements(_template))
        {
            RenombrarRedeclaradas(plantilla, new Dictionary<string, string>(StringComparer.Ordinal), redeclaradas);
        }

        var agregadas = new List<string>();
        foreach (var respaldo in respaldos)
        {
            raiz.AddFirst(new XElement(_variable, new XAttribute("name", respaldo.Variable), new XAttribute("select", respaldo.Valor)));
            agregadas.Add(respaldo.Variable);
        }

        return (xsl, new CorreccionesXsl(concat, redeclaradas, agregadas));
    }

    private static int CorregirConcat(XElement raiz)
    {
        var total = 0;
        // Expresiones XPath de los elementos XSLT y plantillas de valor ({…}) de los elementos literales.
        foreach (var atributo in raiz.DescendantsAndSelf().Attributes())
        {
            var cambios = 0;
            var valor = ConcatUnArgumento().Replace(atributo.Value, m =>
            {
                cambios++;
                return m.Groups["literal"].Value;
            });
            if (cambios > 0)
            {
                atributo.Value = valor;
                total += cambios;
            }
        }

        return total;
    }

    /// <summary>
    /// Recorre en orden de documento: una variable local es visible para sus hermanos siguientes y los descendientes de
    /// estos. <paramref name="visibles"/> asocia cada nombre original con el nombre vigente en el ámbito.
    /// </summary>
    private static void RenombrarRedeclaradas(XElement contenedor, Dictionary<string, string> visibles, List<string> redeclaradas)
    {
        var ambito = new Dictionary<string, string>(visibles, StringComparer.Ordinal);
        foreach (var hijo in contenedor.Elements())
        {
            RenombrarReferencias(hijo, ambito);
            RenombrarRedeclaradas(hijo, ambito, redeclaradas);
            if (hijo.Name != _variable && hijo.Name != _param)
            {
                continue;
            }

            var nombre = hijo.Attribute("name")?.Value;
            if (nombre is null)
            {
                continue;
            }

            if (ambito.ContainsKey(nombre))
            {
                var nuevo = string.Create(CultureInfo.InvariantCulture, $"{nombre}__pcpe{redeclaradas.Count + 1}");
                hijo.SetAttributeValue("name", nuevo);
                ambito[nombre] = nuevo;
                redeclaradas.Add(nombre);
            }
            else
            {
                ambito[nombre] = nombre;
            }
        }
    }

    private static void RenombrarReferencias(XElement elemento, Dictionary<string, string> ambito)
    {
        var renombradas = ambito.Where(p => !string.Equals(p.Key, p.Value, StringComparison.Ordinal)).ToList();
        if (renombradas.Count == 0)
        {
            return;
        }

        foreach (var atributo in elemento.Attributes())
        {
            var valor = atributo.Value;
            foreach (var (original, vigente) in renombradas)
            {
                valor = Regex.Replace(valor, @"\$" + Regex.Escape(original) + @"(?![\w.\-])", "$" + vigente, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }

            atributo.Value = valor;
        }
    }

    [GeneratedRegex("""(?<![\w.:\-])concat\(\s*(?<literal>'[^']*'|"[^"]*")\s*\)""", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ConcatUnArgumento();
}

/// <summary>Correcciones aplicadas a una XSL.</summary>
internal sealed record CorreccionesXsl(int ConcatUnArgumento, IReadOnlyList<string> VariablesRedeclaradas, IReadOnlyList<string> VariablesRespaldo);

/// <summary>Variable global que se agrega a una XSL porque la usa sin declararla.</summary>
internal sealed record VariableRespaldoXsl(string Xsl, string Variable, string Valor, string Motivo);
