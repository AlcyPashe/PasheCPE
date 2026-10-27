using PasheCPE.Pdf.Representacion;

namespace PasheCPE.Pdf.Tests.Representacion;

public sealed class FormatoImpresionTests
{
    [Theory]
    [InlineData(1234.5, "PEN", "S/ 1,234.50")]
    [InlineData(0, "PEN", "S/ 0.00")]
    [InlineData(1234567.891, "USD", "US$ 1,234,567.89")]
    [InlineData(10, "EUR", "€ 10.00")]
    [InlineData(10, "GBP", "GBP 10.00")]
    public void Monto_SimboloYSeparadorDeMiles(decimal monto, string moneda, string esperado) =>
        Assert.Equal(esperado, FormatoImpresion.Monto(monto, moneda));

    [Theory]
    [InlineData("20.0000000000", "20")]
    [InlineData("2.5", "2.5")]
    [InlineData("1000.125", "1,000.125")]
    public void Cantidad_SinCerosSobrantes(string cantidad, string esperado) =>
        Assert.Equal(esperado, FormatoImpresion.Cantidad(decimal.Parse(cantidad, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("1.9600000000", "1.96")]
    [InlineData("0.123456", "0.123456")]
    [InlineData("1500", "1,500.00")]
    public void Precio_DosDecimalesComoMinimo(string precio, string esperado) =>
        Assert.Equal(esperado, FormatoImpresion.Precio(decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Fecha_DdMmAaaa() => Assert.Equal("05/01/2027", FormatoImpresion.Fecha(new DateOnly(2027, 1, 5)));

    [Theory]
    [InlineData("6", "RUC")]
    [InlineData("1", "DNI")]
    [InlineData("Z", "Z")]
    public void DocumentoIdentidad_Abreviatura(string tipo, string esperado) =>
        Assert.Equal(esperado, FormatoImpresion.DocumentoIdentidad(tipo));

    [Fact]
    public void Titulos_SoloDocumentosConDiseno()
    {
        Assert.Equal("NOTA DE DÉBITO ELECTRÓNICA", FormatoImpresion.Titulo("08"));
        Assert.All(["01", "03", "07", "08", "09"], t => Assert.True(FormatoImpresion.TieneDiseno(t)));
        Assert.All(["RC", "RA", "31", "20"], t => Assert.False(FormatoImpresion.TieneDiseno(t)));
    }
}
