using System.Text;
using System.Xml;
using System.Xml.Linq;
using PasheCPE.Mapeo;

namespace PasheCPE.Tests.Utilidades;

/// <summary>Genera el XML de un caso tributario y permite alterarlo para probar las validaciones de SUNAT.</summary>
internal static class XmlDePrueba
{
    public static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>XML sin firmar del caso (sin validar contra SUNAT: se genera aunque tenga hallazgos).</summary>
    public static async Task<(byte[] Xml, ReferenciaCpe Referencia)> GenerarAsync(ServiciosPrueba servicios, string caso)
    {
        var datos = CasosTributarios.Todos[caso];
        var generado = await servicios.Obtener<GeneradorInvoice>().GenerarAsync(datos.Emisor, datos.Comprobante, TestContext.Current.CancellationToken);
        return (generado.Xml ?? throw new InvalidOperationException(string.Join("; ", generado.Errores.Select(e => $"{e.Codigo} {e.Descripcion}"))), generado.Referencia);
    }

    /// <summary>Aplica la alteración y devuelve el XML en UTF-8 sin BOM, sin reindentar.</summary>
    public static byte[] Alterar(byte[] xml, Action<XElement> alteracion)
    {
        ArgumentNullException.ThrowIfNull(alteracion);
        XDocument documento;
        using (var memoria = new MemoryStream(xml))
        {
            documento = XDocument.Load(memoria, LoadOptions.PreserveWhitespace);
        }

        alteracion(documento.Root!);
        using var salida = new MemoryStream();
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false }))
        {
            documento.Save(escritor);
        }

        return salida.ToArray();
    }

    /// <summary>Primer descendiente con la ruta de nombres indicada (cac:… / cbc:…).</summary>
    public static XElement Elemento(this XElement raiz, params XName[] ruta)
    {
        var actual = raiz;
        foreach (var nombre in ruta)
        {
            actual = actual.Element(nombre) ?? throw new InvalidOperationException($"No existe {nombre} en {actual.Name}.");
        }

        return actual;
    }

    /// <summary>Subtotal del tributo en el TaxTotal indicado (cabecera o línea).</summary>
    public static XElement Subtotal(this XElement contenedor, string tributo) =>
        contenedor.Element(Cac + "TaxTotal")!.Elements(Cac + "TaxSubtotal")
            .First(s => s.Element(Cac + "TaxCategory")!.Element(Cac + "TaxScheme")!.Element(Cbc + "ID")!.Value == tributo);

    public static XElement PrimeraLinea(this XElement raiz) => raiz.Element(Cac + "InvoiceLine")!;
}
