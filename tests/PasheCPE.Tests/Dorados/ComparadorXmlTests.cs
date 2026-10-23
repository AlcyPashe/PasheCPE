using System.Xml.Linq;

namespace PasheCPE.Tests.Dorados;

public sealed class ComparadorXmlTests
{
    private static readonly HashSet<string> _sinIgnorar = new(StringComparer.Ordinal);

    [Fact]
    public void Comparar_NumerosEquivalentesYEspacios_NoSonDiferencia() =>
        Assert.Empty(Comparar(
            "<cbc:LineCountNumeric>8.00</cbc:LineCountNumeric><cbc:Line>  Av.   Freyre </cbc:Line>",
            "<cbc:LineCountNumeric>8</cbc:LineCountNumeric><cbc:Line>Av. Freyre</cbc:Line>"));

    [Fact]
    public void Comparar_ValorDistinto_EsDiferencia()
    {
        var diferencia = Assert.Single(Comparar("<cbc:PayableAmount>10.00</cbc:PayableAmount>", "<cbc:PayableAmount>10.01</cbc:PayableAmount>"));

        Assert.Equal(("Valor", "/Invoice/cbc:PayableAmount[1]"), (diferencia.Tipo, diferencia.Ruta));
    }

    [Fact]
    public void Comparar_LeyendasPorCodigo_NoPorPosicion()
    {
        var diferencias = Comparar(
            "<cbc:Note languageLocaleID=\"1002\">GRATUITA</cbc:Note>",
            "<cbc:Note languageLocaleID=\"1000\">CIEN</cbc:Note><cbc:Note languageLocaleID=\"1002\">GRATUITA</cbc:Note>");

        Assert.Equal(("ElementoAdicional", "/Invoice/cbc:Note[1000]"), (Assert.Single(diferencias).Tipo, diferencias[0].Ruta));
    }

    [Fact]
    public void Comparar_ElementoYAtributoFaltantes_SonDiferencia()
    {
        var diferencias = Comparar("<cbc:ID schemeID=\"6\">1</cbc:ID><cbc:Name>X</cbc:Name>", "<cbc:ID>1</cbc:ID>");

        Assert.Contains(diferencias, d => d.Tipo == "AtributoFaltante" && d.Ruta == "/Invoice/cbc:ID[1]/@schemeID");
        Assert.Contains(diferencias, d => d.Tipo == "ElementoFaltante" && d.Ruta == "/Invoice/cbc:Name[1]");
    }

    private static IReadOnlyList<Diferencia> Comparar(string esperado, string obtenido) =>
        ComparadorXml.Comparar(Raiz(esperado), Raiz(obtenido), _sinIgnorar);

    private static XElement Raiz(string contenido) =>
        XElement.Parse($"<Invoice xmlns:cbc=\"urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2\">{contenido}</Invoice>");
}
