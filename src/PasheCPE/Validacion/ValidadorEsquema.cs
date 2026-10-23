using System.Globalization;
using System.Xml;
using System.Xml.Schema;
using PasheCPE.Catalogos;
using PasheCPE.Ubl;

namespace PasheCPE.Validacion;

/// <summary>
/// Validación contra los XSD oficiales UBL 2.1 de SUNAT (TRD §9.2), incrustados desde <c>recursos/XSD</c>. El
/// conjunto se compila una sola vez. Se valida el XML sin firmar: el <c>ext:UBLExtensions</c> vacío (preparado para
/// la firma) se excluye, porque el XSD exige un elemento dentro de <c>ExtensionContent</c>.
/// </summary>
internal sealed class ValidadorEsquema(CatalogoErrores errores)
{
    private static readonly string[] _documentos = ["Invoice", "CreditNote", "DebitNote", "DespatchAdvice", "ApplicationResponse"];

    private readonly Lazy<XmlSchemaSet> _esquemas = new(Compilar);

    /// <summary>Errores de esquema del XML (vacío si es válido). Nunca lanza por un XML inválido o mal formado.</summary>
    public IReadOnlyList<ErrorCpe> Validar(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var encontrados = new List<ErrorCpe>();
        XmlDocument documento;
        try
        {
            documento = Cargar(xml);
        }
        catch (XmlException ex)
        {
            encontrados.Add(Error(ex.Message, null));
            return encontrados;
        }

        QuitarExtensionesVacias(documento);
        var ruta = new RutaXml();
        var opciones = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = _esquemas.Value,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };
        opciones.ValidationEventHandler += (_, e) =>
        {
            if (e.Severity == XmlSeverityType.Error)
            {
                encontrados.Add(Error(e.Message, ruta.Actual));
            }
        };

        using var lector = XmlReader.Create(new XmlNodeReader(documento), opciones);
        while (lector.Read())
        {
            ruta.Avanzar(lector);
        }

        return encontrados;
    }

    private ErrorCpe Error(string mensaje, string? ruta) =>
        errores.Local("PCPE-2001", OrigenError.Esquema, ruta, null, mensaje);

    private static XmlDocument Cargar(byte[] xml)
    {
        var documento = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var memoria = new MemoryStream(xml, writable: false);
        using var lector = XmlReader.Create(memoria, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        documento.Load(lector);
        return documento;
    }

    private static void QuitarExtensionesVacias(XmlDocument documento)
    {
        var espacios = new XmlNamespaceManager(documento.NameTable);
        espacios.AddNamespace("ext", EspaciosUbl.Ext);
        if (documento.DocumentElement?.SelectSingleNode("ext:UBLExtensions", espacios) is XmlElement extensiones
            && extensiones.SelectNodes("ext:UBLExtension/ext:ExtensionContent/*", espacios)?.Count == 0)
        {
            documento.DocumentElement.RemoveChild(extensiones);
        }
    }

    /// <summary>
    /// Agrega primero todos los XSD con su URI interna (pashecpe://xsd/…), así los import ya están cargados y nunca se
    /// resuelve nada fuera de los recursos. El xmldsig de UBL trae un DOCTYPE con entidades internas: se procesa sin
    /// resolver externo.
    /// </summary>
    private static XmlSchemaSet Compilar()
    {
        var resolutor = new ResolutorXsd();
        var esquemas = new XmlSchemaSet { XmlResolver = resolutor };
        var opciones = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, MaxCharactersFromEntities = 1024 };
        var nombres = typeof(ValidadorEsquema).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResolutorXsd.PrefijoRecurso + "2.1.common.", StringComparison.Ordinal))
            .OrderBy(n => n.Contains("xmldsig", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(n => n, StringComparer.Ordinal)
            .Concat(_documentos.Select(d => $"{ResolutorXsd.PrefijoRecurso}2.1.maindoc.UBL-{d}-2.1.xsd"));
        foreach (var nombre in nombres)
        {
            using var flujo = RecursosEmbebidos.Abrir(nombre["PasheCPE.Recursos.".Length..]);
            using var lector = XmlReader.Create(flujo, opciones, ResolutorXsd.Uri(nombre).AbsoluteUri);
            esquemas.Add(null, lector);
        }

        esquemas.Compile();
        return esquemas;
    }

    /// <summary>Ruta XPath del elemento que se está leyendo, con índices: /Invoice/cac:InvoiceLine[2]/cbc:ID[1].</summary>
    private sealed class RutaXml
    {
        private readonly List<(string Nombre, Dictionary<string, int> Hijos)> _pila = [];
        private bool _cerrarAlAvanzar;

        public string? Actual => _pila.Count == 0 ? null : "/" + string.Join('/', _pila.Select(p => p.Nombre));

        public void Avanzar(XmlReader lector)
        {
            if (_cerrarAlAvanzar)
            {
                _pila.RemoveAt(_pila.Count - 1);
                _cerrarAlAvanzar = false;
            }

            if (lector.NodeType == XmlNodeType.Element)
            {
                var nombre = lector.Name;
                if (_pila.Count > 0)
                {
                    var hijos = _pila[^1].Hijos;
                    hijos[nombre] = hijos.GetValueOrDefault(nombre) + 1;
                    nombre = string.Create(CultureInfo.InvariantCulture, $"{nombre}[{hijos[nombre]}]");
                }

                _pila.Add((nombre, new Dictionary<string, int>(StringComparer.Ordinal)));
                _cerrarAlAvanzar = lector.IsEmptyElement;
            }
            else if (lector.NodeType == XmlNodeType.EndElement)
            {
                _cerrarAlAvanzar = true;
            }
        }
    }
}

/// <summary>Entrega a XmlSchemaSet solo los XSD incrustados; cualquier otra URI se rechaza (sin acceso a red ni disco).</summary>
internal sealed class ResolutorXsd : XmlResolver
{
    public const string PrefijoRecurso = "PasheCPE.Recursos.xsd.";
    private const string _esquema = "pashecpe";

    /// <summary>URI interna de un recurso: PasheCPE.Recursos.xsd.2.1.common.X.xsd → pashecpe://xsd/2.1/common/X.xsd.</summary>
    public static Uri Uri(string recurso)
    {
        var relativo = recurso[PrefijoRecurso.Length..];
        var partes = relativo.Split('.');
        // 2.1.common.UBL-...-2.1.xsd: los dos primeros segmentos son la versión, el tercero la carpeta.
        var archivo = string.Join('.', partes[3..]);
        return new Uri($"{_esquema}://xsd/{partes[0]}.{partes[1]}/{partes[2]}/{archivo}");
    }

    public override object? GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
    {
        ArgumentNullException.ThrowIfNull(absoluteUri);
        if (absoluteUri.Scheme != _esquema)
        {
            throw new XmlException($"No se permite resolver '{absoluteUri}': solo se usan los XSD incrustados.");
        }

        var nombre = "xsd" + absoluteUri.AbsolutePath.Replace('/', '.');
        return RecursosEmbebidos.Abrir(nombre);
    }
}
