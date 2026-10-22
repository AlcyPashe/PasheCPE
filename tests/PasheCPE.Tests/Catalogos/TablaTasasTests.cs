using PasheCPE.Catalogos;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Catalogos;

public sealed class TablaTasasTests : IDisposable
{
    private readonly AlmacenTemporal _almacen = new();

    public void Dispose() => _almacen.Dispose();

    [Theory]
    [InlineData("2025-06-30", 18.00, 16.00, 2.00)]
    [InlineData("2026-01-01", 18.00, 15.50, 2.50)]
    [InlineData("2026-12-31", 18.00, 15.50, 2.50)]
    [InlineData("2027-01-01", 18.00, 15.00, 3.00)]
    [InlineData("2028-03-15", 18.00, 14.50, 3.50)]
    [InlineData("2029-01-01", 18.00, 14.00, 4.00)]
    public void Igv_SegunFechaDeEmision(string fecha, double tasa, double igv, double ipm)
    {
        var tramo = new TablaTasas(_almacen.OpcionesDi()).Igv(DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal((decimal)tasa, tramo.Tasa);
        Assert.Equal((decimal)igv, tramo.Igv);
        Assert.Equal((decimal)ipm, tramo.Ipm);
    }

    [Theory]
    [InlineData("2024-12-31", null)]
    [InlineData("2025-01-01", 10.00)]
    [InlineData("2026-07-01", 10.50)]
    [InlineData("2027-12-31", 15.00)]
    [InlineData("2028-01-01", null)]
    public void MypeRestaurantesHoteles_SegunFechaDeEmision(string fecha, double? tasa) =>
        Assert.Equal(
            tasa is null ? null : (decimal)tasa.Value,
            new TablaTasas(_almacen.OpcionesDi()).MypeRestaurantesHoteles(DateOnly.Parse(fecha, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Icbper_Vigente_EsCincuentaCentimos() =>
        Assert.Equal(0.50m, new TablaTasas(_almacen.OpcionesDi()).Icbper(new DateOnly(2026, 10, 22)));

    [Fact]
    public void Icbper_AntesDelPrimerTramo_EsNull() =>
        Assert.Null(new TablaTasas(_almacen.OpcionesDi()).Icbper(new DateOnly(2022, 12, 31)));

    [Fact]
    public void Igv_ConTramosConfigurados_ReemplazaLosEmbebidos()
    {
        var opciones = _almacen.OpcionesDi(o => o.Tasas.Igv.Add(new TramoIgv { Desde = new DateOnly(2000, 1, 1), Tasa = 19m, Igv = 17m, Ipm = 2m }));

        Assert.Equal(19m, new TablaTasas(opciones).Igv(new DateOnly(2027, 1, 1)).Tasa);
    }
}
