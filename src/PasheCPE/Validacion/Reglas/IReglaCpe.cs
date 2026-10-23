using System.Globalization;
using System.Xml;
using System.Xml.XPath;
using PasheCPE.Ubl;

namespace PasheCPE.Validacion.Reglas;

/// <summary>
/// Regla complementaria a las XSL de SUNAT (TRD §9.4): implementa en C# filas del Excel de reglas vigente que la XSL
/// (06/09/2022) no tiene o calcula distinto. Evalúa el XML, no el modelo, así que sirve para cualquier XML. El código y
/// el tipo (error u observación según la vigencia) salen de la fila del Excel; la regla solo informa la fila que falla.
/// </summary>
internal interface IReglaCpe
{
    /// <summary>Nombre con el que <c>ajustes-xsl.json</c> la referencia.</summary>
    string Nombre { get; }

    /// <summary>Filas del Excel que implementa.</summary>
    IReadOnlyList<FilaExcel> Filas { get; }

    /// <summary>Filas que no se cumplen en el documento (solo del tipo de documento que se evalúa).</summary>
    IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento);
}

/// <summary>Fila de una hoja de reglas del Excel de SUNAT.</summary>
internal readonly record struct FilaExcel(string Documento, int Fila);

/// <summary>Fila del Excel que no se cumple, con la ruta del nodo y el detalle del cálculo.</summary>
internal sealed record HallazgoRegla(int Fila, string? Ruta, string Detalle);

/// <summary>XML de un comprobante preparado para las reglas: navegación XPath con los prefijos UBL.</summary>
internal sealed class DocumentoXml
{
    private readonly XPathNavigator _raiz;
    private readonly XmlNamespaceManager _espacios;

    private DocumentoXml(XPathNavigator raiz, string tipoDocumento)
    {
        _raiz = raiz;
        TipoDocumento = tipoDocumento;
        _espacios = new XmlNamespaceManager(raiz.NameTable);
        _espacios.AddNamespace("cac", EspaciosUbl.Cac);
        _espacios.AddNamespace("cbc", EspaciosUbl.Cbc);
        _espacios.AddNamespace("ext", EspaciosUbl.Ext);
        _espacios.AddNamespace("sac", EspaciosUbl.Sac);
        TipoOperacion = Texto("/*/cbc:InvoiceTypeCode/@listID");
    }

    public string TipoDocumento { get; }

    /// <summary>Tipo de operación (catálogo N.° 51) de facturas y boletas.</summary>
    public string? TipoOperacion { get; }

    public static DocumentoXml Cargar(byte[] xml, string tipoDocumento)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var memoria = new MemoryStream(xml, writable: false);
        using var lector = XmlReader.Create(memoria, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var navegador = new XPathDocument(lector).CreateNavigator();
        return new DocumentoXml(navegador, tipoDocumento);
    }

    public IEnumerable<XPathNavigator> Nodos(string xpath, XPathNavigator? contexto = null)
    {
        foreach (XPathNavigator nodo in (contexto ?? _raiz).Select(xpath, _espacios))
        {
            yield return nodo.Clone();
        }
    }

    public XPathNavigator? Nodo(string xpath, XPathNavigator? contexto = null) => (contexto ?? _raiz).SelectSingleNode(xpath, _espacios);

    public bool Existe(string xpath, XPathNavigator? contexto = null) => Nodo(xpath, contexto) is not null;

    public string? Texto(string xpath, XPathNavigator? contexto = null) => Nodo(xpath, contexto)?.Value;

    /// <summary>Valor decimal del nodo; null si no existe o no es un número.</summary>
    public decimal? Decimal(string xpath, XPathNavigator? contexto = null) =>
        Texto(xpath, contexto) is { } texto && decimal.TryParse(texto.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;

    /// <summary>Suma de los nodos numéricos (los que no son números no suman).</summary>
    public decimal Suma(string xpath, XPathNavigator? contexto = null) =>
        Nodos(xpath, contexto)
            .Select(n => decimal.TryParse(n.Value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : 0m)
            .Sum();

    /// <summary>Ruta XPath del nodo con índices: /Invoice/cac:TaxTotal[1]/cbc:TaxAmount[1].</summary>
    public static string Ruta(XPathNavigator nodo)
    {
        ArgumentNullException.ThrowIfNull(nodo);
        var partes = new List<string>();
        var actual = nodo.Clone();
        if (actual.NodeType == XPathNodeType.Attribute)
        {
            partes.Add("@" + actual.Name);
            actual.MoveToParent();
        }

        while (actual.NodeType == XPathNodeType.Element)
        {
            var padre = actual.Clone();
            var esRaiz = !padre.MoveToParent() || padre.NodeType != XPathNodeType.Element;
            partes.Add(esRaiz ? actual.Name : string.Create(CultureInfo.InvariantCulture, $"{actual.Name}[{Posicion(actual)}]"));
            if (!actual.MoveToParent())
            {
                break;
            }
        }

        partes.Reverse();
        return "/" + string.Join('/', partes);
    }

    private static int Posicion(XPathNavigator nodo)
    {
        var hermano = nodo.Clone();
        var posicion = 1;
        while (hermano.MoveToPrevious())
        {
            if (hermano.NodeType == XPathNodeType.Element && hermano.LocalName == nodo.LocalName && hermano.NamespaceURI == nodo.NamespaceURI)
            {
                posicion++;
            }
        }

        return posicion;
    }
}
