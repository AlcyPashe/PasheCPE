using System.Xml.Linq;
using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Dorados;

/// <summary>
/// Compara el XML que genera PasheCPE con facturas aceptadas por SUNAT (Fattura v2, GasperSoft): se arma el modelo
/// desde cada dorado, se genera y se compara por estructura y valores. Las diferencias esperadas están documentadas
/// en DIFERENCIAS.md; cualquier otra hace fallar la prueba.
/// </summary>
public sealed class DoradosInvoiceTests : IDisposable
{
    /// <summary>Elementos que dependen de la firma y no se comparan.</summary>
    private static readonly HashSet<string> _ignorados = new(StringComparer.Ordinal) { "ext:UBLExtensions", "cac:Signature" };

    /// <summary>Diferencias esperadas (DIFERENCIAS.md): tipo y ruta.</summary>
    private static readonly (string Tipo, string Ruta)[] _esperadas =
    [
        ("ElementoAdicional", "/Invoice/cbc:Note[1000]"),
        ("ElementoAdicional", "/Invoice/cbc:Note[2001]"),
        ("AtributoAdicional", "/Invoice/cbc:InvoiceTypeCode[1]/@name"),
        ("AtributoAdicional", "/Invoice/cbc:InvoiceTypeCode[1]/@listSchemeURI"),
    ];

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    public static TheoryData<string> Facturas() =>
        [.. Directory.GetFiles(LectorDorado.Carpeta("01-Factura"), "*.xml").Select(r => Path.GetFileName(r)).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Facturas))]
    public async Task Generar_DesdeFacturaAceptada_SoloDifiereEnLoDocumentado(string archivo)
    {
        var dorado = LectorDorado.Cargar(Path.Combine(LectorDorado.Carpeta("01-Factura"), archivo));
        var (emisor, comprobante) = LectorDorado.Invoice(dorado);

        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(emisor, comprobante, TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito, string.Join(Environment.NewLine, resultado.Errores.Select(e => $"{e.Codigo} {e.Ruta}: {e.Descripcion}")));
        var generado = XDocument.Parse(System.Text.Encoding.UTF8.GetString(resultado.Xml!)).Root!;
        var inesperadas = ComparadorXml.Comparar(dorado, generado, _ignorados)
            .Where(d => !_esperadas.Contains((d.Tipo, d.Ruta)))
            .ToList();

        Assert.True(inesperadas.Count == 0, $"{archivo}:{Environment.NewLine}{string.Join(Environment.NewLine, inesperadas)}");
    }

    [Theory]
    [MemberData(nameof(Facturas))]
    public async Task Generar_DesdeFacturaAceptada_TotalesIguales(string archivo)
    {
        var dorado = LectorDorado.Cargar(Path.Combine(LectorDorado.Carpeta("01-Factura"), archivo));
        var (emisor, comprobante) = LectorDorado.Invoice(dorado);

        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(emisor, comprobante, TestContext.Current.CancellationToken);

        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        var total = dorado.Element(cac + "LegalMonetaryTotal")!;
        Assert.Equal(decimal.Parse(total.Element(cbc + "PayableAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture), resultado.Calculo!.ImporteTotal);
        Assert.Equal(decimal.Parse(total.Element(cbc + "LineExtensionAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture), resultado.Calculo.ValorVenta);
    }
}
