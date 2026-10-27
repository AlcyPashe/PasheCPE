using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PasheCPE.Catalogos;
using PasheCPE.Tests.Dorados;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using PasheCPE.Validacion.Emulacion;

namespace PasheCPE.Tests.Validacion;

/// <summary>
/// Inventario automático de lo que las XSL de SUNAT piden a DataPower (TRD §9.3): plantillas utilitarias y sus
/// parámetros, claves de dp:variable, funciones de extensión, rutas local:///, catálogos y propiedades de catálogo.
/// Todo debe estar emulado; si SUNAT publica una XSL que pide algo nuevo, esta prueba falla.
/// </summary>
public sealed partial class InventarioXslTests : IDisposable
{
    private static readonly XNamespace _xsl = AdaptadorXslDataPower.EspacioXslt;

    private static readonly Dictionary<string, Type> _extensiones = new(StringComparer.Ordinal)
    {
        [ExtensionesDataPower.Espacio] = typeof(ExtensionesDataPower),
        [ExtensionesExsltFechas.Espacio] = typeof(ExtensionesExsltFechas),
        [ExtensionesExsltRegexp.Espacio] = typeof(ExtensionesExsltRegexp),
        [ExtensionesPasheCpe.Espacio] = typeof(ExtensionesPasheCpe),
    };

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    public static TheoryData<string> Xsl => [.. MotorXslSunat.XslEmbebidas];

    private static XDocument Utilidades => XDocument.Load(Path.Combine(LectorDorado.Raiz(), "src", "PasheCPE", "Recursos", "xsl-utils", "validate_utils.xsl"));

    private static XDocument Cargar(string archivo) => XDocument.Load(LectorDorado.RutaXsl(archivo));

    [Theory]
    [MemberData(nameof(Xsl))]
    public void Plantillas_LlamadasYParametros_EstanEnLasUtilidades(string archivo)
    {
        var xsl = Cargar(archivo);
        var propias = xsl.Root!.Elements(_xsl + "template").Select(t => (string?)t.Attribute("name")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var utilidades = Utilidades.Root!.Elements(_xsl + "template")
            .Where(t => t.Attribute("name") is not null)
            .ToDictionary(t => (string)t.Attribute("name")!, t => t.Elements(_xsl + "param").Select(p => (string)p.Attribute("name")!).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        foreach (var llamada in xsl.Descendants(_xsl + "call-template"))
        {
            var nombre = (string)llamada.Attribute("name")!;
            if (propias.Contains(nombre))
            {
                continue;
            }

            Assert.True(utilidades.TryGetValue(nombre, out var parametros), $"{archivo}: la plantilla '{nombre}' no está en validate_utils.xsl.");
            foreach (var parametro in llamada.Elements(_xsl + "with-param").Select(p => (string)p.Attribute("name")!))
            {
                Assert.True(parametros.Contains(parametro), $"{archivo}: la plantilla '{nombre}' no declara el parámetro '{parametro}'.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Xsl))]
    public void Extensiones_FuncionesYClavesDataPower_EstanEmuladas(string archivo)
    {
        foreach (var (prefijo, funcion, elemento, argumento) in LlamadasDeExtension(Cargar(archivo)))
        {
            var espacio = elemento.GetNamespaceOfPrefix(prefijo)?.NamespaceName ?? string.Empty;
            Assert.True(_extensiones.TryGetValue(espacio, out var tipo), $"{archivo}: {prefijo}:{funcion} usa el espacio '{espacio}', que no se emula.");
            Assert.True(tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m => m.Name == funcion), $"{archivo}: falta {prefijo}:{funcion} en {tipo.Name}.");
            if (tipo == typeof(ExtensionesDataPower))
            {
                Assert.Contains(argumento, ExtensionesDataPower.Claves);
            }
        }
    }

    [Fact]
    public void Extensiones_LasQueUsanLasUtilidades_EstanEmuladas()
    {
        foreach (var (prefijo, funcion, elemento, _) in LlamadasDeExtension(Utilidades))
        {
            var espacio = elemento.GetNamespaceOfPrefix(prefijo)!.NamespaceName;
            Assert.True(_extensiones[espacio].GetMethods().Any(m => m.Name == funcion), $"Falta {prefijo}:{funcion}.");
        }
    }

    [Theory]
    [MemberData(nameof(Xsl))]
    public void RutasLocales_SeResuelvenConRecursosEmbebidos(string archivo)
    {
        var texto = Cargar(archivo).ToString();

        Assert.All(RutaLocal().Matches(texto).Select(m => m.Value), ruta => Assert.Contains(ruta, ResolutorRecursosLocales.Rutas));
    }

    [Theory]
    [MemberData(nameof(Xsl))]
    public void Catalogos_YSusPropiedades_ExistenEnPasheCpe(string archivo)
    {
        var xsl = Cargar(archivo);
        var catalogos = _servicios.Obtener<CatalogosSunat>();
        var tributos = xsl.Descendants(_xsl + "variable").Where(v => (string?)v.Attribute("name") == "codTributo")
            .Descendants(_xsl + "value-of").Select(v => ((string)v.Attribute("select")!).Trim('\''))
            .Where(v => v.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var llamada in xsl.Descendants(_xsl + "call-template").Where(c => ((string)c.Attribute("name")!).StartsWith("findElementInCatalog", StringComparison.Ordinal)))
        {
            var parametros = llamada.Elements(_xsl + "with-param").ToDictionary(p => (string)p.Attribute("name")!, p => (string?)p.Attribute("select") ?? string.Empty, StringComparer.Ordinal);
            var catalogo = parametros["catalogo"].Trim('\'');
            Assert.False(string.IsNullOrEmpty(catalogos.Nombre(catalogo)));
            if (parametros.TryGetValue("propiedad", out var propiedad))
            {
                var propiedades = propiedad == "$codTributo" ? tributos : [propiedad.Trim('\'')];
                Assert.True(ExtensionesPasheCpe.Propiedades.TryGetValue(catalogo, out var conocidas), $"{archivo}: el catálogo {catalogo} no tiene propiedades emuladas.");
                Assert.All(propiedades, p => Assert.Contains(p, conocidas));
            }
        }
    }

    [Fact]
    public void Propiedades_DevuelvenLosValoresDelExcel()
    {
        var extension = new ExtensionesPasheCpe(_servicios.Obtener<CatalogosSunat>(), new DateOnly(2026, 10, 22));

        Assert.Equal("IGV", extension.propiedadCatalogo("05", "1000", "name"));
        Assert.Equal("VAT", extension.propiedadCatalogo("05", "1000", "UN_ECE_5153"));
        Assert.Equal("1", extension.propiedadCatalogo("07", "10", "igv"));
        Assert.Equal("0", extension.propiedadCatalogo("07", "10", "exo"));
        Assert.Equal("1", extension.propiedadCatalogo("07", "17", "iva"));
        Assert.Equal("1", extension.propiedadCatalogo("07", "17", "gra"));
        Assert.Equal("2", extension.propiedadCatalogo("22", "01", "tasa"));
        Assert.Equal("1", extension.propiedadCatalogo("51", "0101", "boleta"));
        Assert.Equal("0", extension.propiedadCatalogo("51", "0113", "factura"));
        Assert.Equal(string.Empty, extension.propiedadCatalogo("05", "1234", "name"));
        Assert.Throws<PropiedadCatalogoDesconocidaException>(() => extension.propiedadCatalogo("05", "1000", "otra"));
    }

    private static IEnumerable<(string Prefijo, string Funcion, XElement Elemento, string? Argumento)> LlamadasDeExtension(XDocument xsl)
    {
        foreach (var elemento in xsl.Descendants())
        {
            foreach (var atributo in elemento.Attributes())
            {
                foreach (Match llamada in FuncionConPrefijo().Matches(atributo.Value))
                {
                    yield return (llamada.Groups["prefijo"].Value, llamada.Groups["funcion"].Value, elemento, llamada.Groups["argumento"].Success ? llamada.Groups["argumento"].Value : null);
                }
            }
        }
    }

    [GeneratedRegex(@"(?<![\w$\-])(?<prefijo>[A-Za-z][\w\-]*):(?<funcion>[A-Za-z][\w\-]*)\(\s*(?:'(?<argumento>[^']*)')?")]
    private static partial Regex FuncionConPrefijo();

    [GeneratedRegex(@"local:///[^'""\)\s]+")]
    private static partial Regex RutaLocal();
}
