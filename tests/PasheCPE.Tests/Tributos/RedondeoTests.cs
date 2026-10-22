using PasheCPE.Tributos;

namespace PasheCPE.Tests.Tributos;

public sealed class RedondeoTests
{
    [Theory]
    [InlineData(2.345, 2.35)]
    [InlineData(2.344, 2.34)]
    [InlineData(-2.345, -2.35)]
    [InlineData(0.005, 0.01)]
    public void Monto_RedondeaADosDecimalesAlejandoseDeCero(double valor, double esperado) =>
        Assert.Equal((decimal)esperado, Redondeo.Monto((decimal)valor));

    [Fact]
    public void Unitario_ConservaDiezDecimales() =>
        Assert.Equal(0.8474576271m, Redondeo.Unitario(1m / 1.18m));
}
