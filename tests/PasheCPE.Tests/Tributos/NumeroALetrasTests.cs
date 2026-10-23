using PasheCPE.Tributos;

namespace PasheCPE.Tests.Tributos;

public sealed class NumeroALetrasTests
{
    [Theory]
    [InlineData("0", "CERO CON 00/100 SOLES")]
    [InlineData("1", "UNO CON 00/100 SOLES")]
    [InlineData("15", "QUINCE CON 00/100 SOLES")]
    [InlineData("16", "DIECISEIS CON 00/100 SOLES")]
    [InlineData("21", "VEINTIUNO CON 00/100 SOLES")]
    [InlineData("22", "VEINTIDOS CON 00/100 SOLES")]
    [InlineData("31", "TREINTA Y UNO CON 00/100 SOLES")]
    [InlineData("100", "CIEN CON 00/100 SOLES")]
    [InlineData("101", "CIENTO UNO CON 00/100 SOLES")]
    [InlineData("120.50", "CIENTO VEINTE CON 50/100 SOLES")]
    [InlineData("1000", "MIL CON 00/100 SOLES")]
    [InlineData("1001", "MIL UNO CON 00/100 SOLES")]
    [InlineData("21000", "VEINTIUN MIL CON 00/100 SOLES")]
    [InlineData("31000", "TREINTA Y UN MIL CON 00/100 SOLES")]
    [InlineData("101000", "CIENTO UN MIL CON 00/100 SOLES")]
    [InlineData("1000000", "UN MILLON CON 00/100 SOLES")]
    [InlineData("2500000.75", "DOS MILLONES QUINIENTOS MIL CON 75/100 SOLES")]
    [InlineData("21000000", "VEINTIUN MILLONES CON 00/100 SOLES")]
    [InlineData("1000000000", "MIL MILLONES CON 00/100 SOLES")]
    [InlineData("999999999999.99", "NOVECIENTOS NOVENTA Y NUEVE MIL NOVECIENTOS NOVENTA Y NUEVE MILLONES NOVECIENTOS NOVENTA Y NUEVE MIL NOVECIENTOS NOVENTA Y NUEVE CON 99/100 SOLES")]
    public void Convertir_Soles_DevuelveLeyenda(string monto, string esperado) =>
        Assert.Equal(esperado, NumeroALetras.Convertir(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture), "PEN"));

    [Fact]
    public void Convertir_Dolares_UsaNombreDeLaMoneda() =>
        Assert.Equal("CINCUENTA CON 05/100 DOLARES AMERICANOS", NumeroALetras.Convertir(50.05m, "USD"));

    [Theory]
    [InlineData("16", "PEN", "DIECISÉIS CON 00/100 SOLES")]
    [InlineData("21000", "PEN", "VEINTIÚN MIL CON 00/100 SOLES")]
    [InlineData("1000000", "PEN", "UN MILLÓN CON 00/100 SOLES")]
    [InlineData("50.05", "USD", "CINCUENTA CON 05/100 DÓLARES AMERICANOS")]
    public void Convertir_ConTildes_ParaElPdfUOtrosUsos(string monto, string moneda, string esperado) =>
        Assert.Equal(esperado, NumeroALetras.Convertir(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture), moneda, conTildes: true));

    [Fact]
    public void Convertir_RedondeaLosCentimos() =>
        Assert.Equal("DIEZ CON 01/100 SOLES", NumeroALetras.Convertir(10.005m, "PEN"));

    [Fact]
    public void Convertir_MonedaSinNombre_UsaElCodigo() =>
        Assert.Equal("DIEZ CON 00/100 GBP", NumeroALetras.Convertir(10m, "gbp"));

    [Fact]
    public void Convertir_Negativo_Lanza() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => NumeroALetras.Convertir(-1m, "PEN"));
}
