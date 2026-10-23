using System.Globalization;
using System.Xml.Linq;
using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Dorados;

/// <summary>
/// Compara el XML que genera PasheCPE con notas de crédito aceptadas por SUNAT (GasperSoft): se arma el modelo desde
/// cada dorado, se genera y se compara por estructura y valores. Las diferencias esperadas están documentadas en
/// DIFERENCIAS.md; cualquier otra hace fallar la prueba.
/// </summary>
public sealed class DoradosCreditNoteTests : IDisposable
{
    /// <summary>Elementos que dependen de la firma y no se comparan.</summary>
    private static readonly HashSet<string> _ignorados = new(StringComparer.Ordinal) { "ext:UBLExtensions", "cac:Signature" };

    /// <summary>Diferencias esperadas (DIFERENCIAS.md): tipo y ruta.</summary>
    private static readonly (string Tipo, string Ruta)[] _esperadas =
    [
        ("ElementoAdicional", "/CreditNote/cbc:Note[1000]"),
        ("ElementoAdicional", "/CreditNote/cbc:Note[2001]"),
    ];

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    public static TheoryData<string> Notas() =>
        [.. Directory.GetFiles(LectorDorado.Carpeta("07-NotaCredito"), "*.xml").Select(r => Path.GetFileName(r)).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Notas))]
    public async Task Generar_DesdeNotaAceptada_SoloDifiereEnLoDocumentado(string archivo)
    {
        var dorado = LectorDorado.Cargar(Path.Combine(LectorDorado.Carpeta("07-NotaCredito"), archivo));
        var (emisor, nota) = LectorDorado.CreditNote(dorado);

        var resultado = await _servicios.Obtener<GeneradorNota>().GenerarAsync(emisor, nota, TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito, string.Join(Environment.NewLine, resultado.Errores.Select(e => $"{e.Codigo} {e.Ruta}: {e.Descripcion}")));
        var generado = XDocument.Parse(System.Text.Encoding.UTF8.GetString(resultado.Xml!)).Root!;
        var inesperadas = ComparadorXml.Comparar(dorado, generado, _ignorados)
            .Where(d => !_esperadas.Contains((d.Tipo, d.Ruta)))
            .ToList();

        Assert.True(inesperadas.Count == 0, $"{archivo}:{Environment.NewLine}{string.Join(Environment.NewLine, inesperadas)}");
    }

    [Theory]
    [MemberData(nameof(Notas))]
    public async Task Generar_DesdeNotaAceptada_TotalesIguales(string archivo)
    {
        var dorado = LectorDorado.Cargar(Path.Combine(LectorDorado.Carpeta("07-NotaCredito"), archivo));
        var (emisor, nota) = LectorDorado.CreditNote(dorado);

        var resultado = await _servicios.Obtener<GeneradorNota>().GenerarAsync(emisor, nota, TestContext.Current.CancellationToken);

        XNamespace cbc = XmlDePrueba.Cbc;
        XNamespace cac = XmlDePrueba.Cac;
        var total = dorado.Element(cac + "LegalMonetaryTotal")!;
        Assert.Equal(decimal.Parse(total.Element(cbc + "PayableAmount")!.Value, CultureInfo.InvariantCulture), resultado.Calculo!.ImporteTotal);
        Assert.Equal(decimal.Parse(total.Element(cbc + "LineExtensionAmount")!.Value, CultureInfo.InvariantCulture), resultado.Calculo.ValorVenta);
    }
}
