using PasheCPE.Tests.Utilidades;
using PasheCPE.Tributos;

namespace PasheCPE.Tests.Tributos;

public sealed class CalculadoraTributariaTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    [Fact]
    public void Gravada18_PrecioConImpuestos_MandaElPrecio()
    {
        var calculo = Calcular(Gravado(), Linea(3, 11.80m));

        var linea = Assert.Single(calculo.Lineas);
        Assert.Equal((30.00m, 5.40m), (linea.ValorVenta, linea.Igv));
        Assert.Equal(10.00m, linea.ValorUnitario);
        Assert.Equal(11.80m, linea.PrecioReferencia);
        Assert.Equal(new SubtotalTributo(TributoSunat.Igv, 30.00m, 5.40m), Assert.Single(calculo.Subtotales));
        Assert.Equal((30.00m, 35.40m, 35.40m, 5.40m), (calculo.ValorVenta, calculo.PrecioVenta, calculo.ImporteTotal, calculo.TotalTributos));
        Assert.Equal("TREINTA Y CINCO CON 40/100 SOLES", calculo.Leyendas[0].Texto);
    }

    [Fact]
    public void Gravada18_ValorSinImpuestos()
    {
        var calculo = Calcular(Gravado(), Linea(3, 10m), ModoPrecio.ValorUnitario);

        var linea = Assert.Single(calculo.Lineas);
        Assert.Equal((30.00m, 5.40m, 11.80m), (linea.ValorVenta, linea.Igv, linea.PrecioReferencia));
        Assert.Equal(35.40m, calculo.ImporteTotal);
    }

    [Fact]
    public void ExoneradaAmazonia_PorDefecto_ConLeyenda2001()
    {
        var calculo = Calcular(Ejemplos.Emisor(), Linea(4, 2.50m));

        var linea = Assert.Single(calculo.Lineas);
        Assert.Equal((AfectacionIgv.ExoneradoOneroso, TributoSunat.Exonerado, 10.00m, 0m), (linea.Afectacion, linea.Tributo, linea.ValorVenta, linea.Igv));
        Assert.Equal(new SubtotalTributo(TributoSunat.Exonerado, 10.00m, 0m), Assert.Single(calculo.Subtotales));
        Assert.Equal([CodigoLeyenda.MontoEnLetras, CodigoLeyenda.BienesAmazonia], calculo.Leyendas.Select(l => l.Codigo));
    }

    [Fact]
    public void ExoneradaAmazonia_ServiciosYConstruccion_TienenSusLeyendas()
    {
        var calculo = Calcular(
            Ejemplos.Emisor(),
            Linea(1, 10m) with { TipoItem = TipoItem.Servicio },
            Linea(1, 10m) with { TipoItem = TipoItem.Construccion },
            Linea(1, 10m));

        Assert.Equal(
            [CodigoLeyenda.MontoEnLetras, CodigoLeyenda.BienesAmazonia, CodigoLeyenda.ServiciosAmazonia, CodigoLeyenda.ConstruccionAmazonia],
            calculo.Leyendas.Select(l => l.Codigo));
    }

    [Theory]
    [InlineData(2026, 110.50, 10.50)]
    [InlineData(2027, 115.00, 15.00)]
    public void Mype_UsaLaTasaEspecialDelAnio(int anio, double precio, double igv)
    {
        var calculo = Calcular(Gravado(mype: true), new DateOnly(anio, 6, 15), Linea(1, (decimal)precio));

        Assert.Equal((100.00m, (decimal)igv), (calculo.Lineas[0].ValorVenta, calculo.Lineas[0].Igv));
        Assert.Equal((decimal)igv, calculo.Lineas[0].Tasa);
        Assert.Empty(calculo.Advertencias);
    }

    [Fact]
    public void Mype_SinTasaVigente_AdvierteYUsaLaGeneral()
    {
        var calculo = Calcular(Gravado(mype: true), new DateOnly(2028, 1, 10), Linea(1, 118m));

        Assert.Equal(18.00m, calculo.TasaGravada);
        Assert.Equal("PCPE-1102", Assert.Single(calculo.Advertencias).Codigo);
    }

    [Fact]
    public void Mixta_GravadaExoneradaInafecta_TotalesPorTributo()
    {
        var calculo = Calcular(
            Gravado(),
            Linea(1, 118m),
            Linea(2, 25m) with { Afectacion = AfectacionIgv.ExoneradoOneroso },
            Linea(1, 40m) with { Afectacion = AfectacionIgv.InafectoOneroso });

        Assert.Equal(
            [new SubtotalTributo(TributoSunat.Igv, 100m, 18m), new SubtotalTributo(TributoSunat.Exonerado, 50m, 0m), new SubtotalTributo(TributoSunat.Inafecto, 40m, 0m)],
            calculo.Subtotales);
        Assert.Equal((190m, 208m, 208m), (calculo.ValorVenta, calculo.PrecioVenta, calculo.ImporteTotal));
    }

    [Fact]
    public void GratuitaGravada_IgvReferencialFueraDeLosTotales()
    {
        var calculo = Calcular(
            Gravado(),
            Linea(1, 118m),
            Linea(2, 11.80m) with { Afectacion = AfectacionIgv.GravadoBonificaciones, EsGratuito = true });

        var gratuita = calculo.Lineas[1];
        Assert.True(gratuita.EsGratuita);
        Assert.Equal((20.00m, 3.60m, 0m, 10.00m, TipoPrecio.ValorReferencialGratuito), (gratuita.ValorVenta, gratuita.Igv, gratuita.ValorUnitario, gratuita.PrecioReferencia, gratuita.TipoPrecio));
        Assert.Contains(new SubtotalTributo(TributoSunat.Gratuito, 20.00m, 3.60m), calculo.Subtotales);
        Assert.Equal((100m, 118m, 18m), (calculo.ValorVenta, calculo.ImporteTotal, calculo.TotalTributos));
        Assert.Contains(calculo.Leyendas, l => l.Codigo == CodigoLeyenda.TransferenciaGratuita);
    }

    [Fact]
    public void GratuitaExoneradaAmazonia_SinIgv()
    {
        var calculo = Calcular(Ejemplos.Emisor(), Linea(20, 1.96m) with { EsGratuito = true });

        Assert.Equal((AfectacionIgv.ExoneradoGratuito, 39.20m, 0m), (calculo.Lineas[0].Afectacion, calculo.Lineas[0].ValorVenta, calculo.Lineas[0].Igv));
        Assert.Equal(0m, calculo.ImporteTotal);
    }

    [Theory]
    [InlineData(2026, 2, 1.00)]
    [InlineData(2021, 3, 0.90)]
    public void Icbper_PorBolsaSegunElAnio(int anio, int bolsas, double monto)
    {
        var calculo = Calcular(Gravado(), new DateOnly(anio, 5, 1), Linea(1, 11.80m) with { BolsasIcbper = bolsas });

        Assert.Equal((decimal)monto, calculo.Lineas[0].Icbper);
        Assert.Contains(new SubtotalTributo(TributoSunat.Icbper, 0m, (decimal)monto), calculo.Subtotales);
        Assert.Equal(11.80m + (decimal)monto, calculo.ImporteTotal);
        Assert.Equal(10.00m, calculo.Subtotales[0].Base);
    }

    [Fact]
    public void IscAlValor_EntraEnLaBaseDelIgv()
    {
        var calculo = Calcular(Gravado(), Linea(1, 100m) with { Isc = new IscLineaCpe { Sistema = SistemaIsc.SistemaAlValor, Tasa = 10m } }, ModoPrecio.ValorUnitario);

        Assert.Equal((100m, 10m, 19.80m), (calculo.Lineas[0].ValorVenta, calculo.Lineas[0].Isc, calculo.Lineas[0].Igv));
        Assert.Equal(129.80m, calculo.ImporteTotal);
        Assert.Contains(new SubtotalTributo(TributoSunat.Isc, 100m, 10m), calculo.Subtotales);
    }

    [Fact]
    public void IscMontoFijo_PorUnidad()
    {
        var calculo = Calcular(Gravado(), Linea(2, 50m) with { Isc = new IscLineaCpe { Sistema = SistemaIsc.MontoFijo, MontoPorUnidad = 1.5m } }, ModoPrecio.ValorUnitario);

        Assert.Equal((3.00m, 18.54m), (calculo.Lineas[0].Isc, calculo.Lineas[0].Igv));
        Assert.Equal(121.54m, calculo.ImporteTotal);
    }

    [Fact]
    public void Credito_MontoNetoPendienteEsElImporte()
    {
        var calculo = Calcular(Gravado(), Linea(1, 354m));

        Assert.Equal(354m, calculo.MontoNetoPendiente);
    }

    [Fact]
    public void Detraccion_RestaDelMontoNetoYAgregaLeyenda()
    {
        var cpe = Comprobante(Linea(1, 1180m)) with
        {
            TipoOperacion = "1001",
            Detraccion = new DetraccionCpe { CodigoBienServicio = "037", Porcentaje = 12m, CuentaBancoNacion = "00-000-123456" },
        };

        var calculo = Calcular(Gravado(), cpe);

        Assert.Equal(141.60m, calculo.Detraccion?.Monto);
        Assert.Equal(1038.40m, calculo.MontoNetoPendiente);
        Assert.Contains(calculo.Leyendas, l => l.Codigo == CodigoLeyenda.OperacionSujetaDetraccion);
    }

    [Fact]
    public void AnticipoGravado_ReduceLaBaseYElImporteAPagar()
    {
        var cpe = Comprobante(Linea(1, 1180m)) with
        {
            Anticipos = [new AnticipoCpe { TipoDocumento = DocumentoRelacionadoTributario.FacturaAnticipos, SerieNumero = "F001-1", Monto = 118m, Afectacion = AfectacionIgv.GravadoOneroso }],
        };

        var calculo = Calcular(Gravado(), cpe);

        Assert.Equal(new SubtotalTributo(TributoSunat.Igv, 900m, 162m), calculo.Subtotales[0]);
        Assert.Equal((1000m, 1180m, 118m, 1062m, 162m), (calculo.ValorVenta, calculo.PrecioVenta, calculo.TotalAnticipos, calculo.ImporteTotal, calculo.TotalTributos));
        var deduccion = Assert.Single(calculo.CargosGlobales);
        Assert.Equal((CargoDescuento.AnticipoGravado, 100m, 1000m, 0.1m), (deduccion.Codigo, deduccion.Monto, deduccion.Base, deduccion.Factor));
    }

    [Fact]
    public void AnticipoExonerado_ReduceLaBaseExonerada()
    {
        var cpe = Comprobante(Linea(1, 500m)) with
        {
            Anticipos = [new AnticipoCpe { TipoDocumento = DocumentoRelacionadoTributario.FacturaAnticipos, SerieNumero = "F001-1", Monto = 100m }],
        };

        var calculo = Calcular(Ejemplos.Emisor(), cpe);

        Assert.Equal(new SubtotalTributo(TributoSunat.Exonerado, 400m, 0m), calculo.Subtotales[0]);
        Assert.Equal((CargoDescuento.AnticipoExonerado, 400m), (calculo.CargosGlobales[0].Codigo, calculo.ImporteTotal));
    }

    [Fact]
    public void DescuentoGlobalQueAfectaLaBase_RecalculaElIgv()
    {
        var cpe = Comprobante(Linea(1, 1180m)) with
        {
            DescuentosCargos = [new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoGlobalAfectaBase, Factor = 0.10m }],
        };

        var calculo = Calcular(Gravado(), cpe);

        Assert.Equal(new SubtotalTributo(TributoSunat.Igv, 900m, 162m), calculo.Subtotales[0]);
        Assert.Equal((900m, 1062m, 1062m), (calculo.ValorVenta, calculo.PrecioVenta, calculo.ImporteTotal));
    }

    [Fact]
    public void DescuentosYCargosQueNoAfectanLaBase_SoloCambianElImporte()
    {
        var cpe = Comprobante(Linea(1, 118m)) with
        {
            DescuentosCargos =
            [
                new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoGlobalNoAfectaBase, Monto = 10m },
                new DescuentoCargoCpe { Codigo = CargoDescuento.CargoGlobalNoAfectaBase, EsCargo = true, Monto = 5m },
                new DescuentoCargoCpe { Codigo = CargoDescuento.CargoGlobalAfectaBase, EsCargo = true, Monto = 50m },
            ],
        };

        var calculo = Calcular(Gravado(), cpe);

        Assert.Equal(new SubtotalTributo(TributoSunat.Igv, 150m, 27m), calculo.Subtotales[0]);
        Assert.Equal((150m, 177m, 10m, 5m, 172m), (calculo.ValorVenta, calculo.PrecioVenta, calculo.DescuentosNoBase, calculo.CargosNoBase, calculo.ImporteTotal));
    }

    [Fact]
    public void Exportacion_TributoExp()
    {
        var calculo = Calcular(Gravado(), Comprobante(Linea(1, 300m) with { Afectacion = AfectacionIgv.Exportacion }) with { TipoOperacion = TipoOperacion.ExportacionBienes });

        Assert.Equal(new SubtotalTributo(TributoSunat.Exportacion, 300m, 0m), Assert.Single(calculo.Subtotales));
        Assert.Equal(300m, calculo.ImporteTotal);
    }

    [Fact]
    public void DescuentoDeLinea_ReduceLaBaseYRecalculaElIgv()
    {
        var calculo = Calcular(Gravado(), Linea(2, 59m) with { Descuento = 10m });

        var linea = calculo.Lineas[0];
        Assert.Equal((100m, 10m, 90m, 16.20m, 50m), (linea.ValorBruto, linea.Descuento, linea.ValorVenta, linea.Igv, linea.ValorUnitario));
        Assert.Equal(106.20m, calculo.ImporteTotal);
    }

    [Fact]
    public void Anticipos_MayoresQueElTotal_DevuelveError()
    {
        var cpe = Comprobante(Linea(1, 100m)) with
        {
            Anticipos = [new AnticipoCpe { TipoDocumento = DocumentoRelacionadoTributario.FacturaAnticipos, SerieNumero = "F001-1", Monto = 500m }],
        };

        Assert.Equal("PCPE-1290", Assert.Single(Calcular(Ejemplos.Emisor(), cpe).Errores).Codigo);
    }

    [Theory]
    [InlineData(0.333, 3.00)]
    [InlineData(1.5, 7.99)]
    [InlineData(7, 0.10)]
    [InlineData(1000, 0.10)]
    [InlineData(0.001, 999.99)]
    [InlineData(12.345, 1.23)]
    public void CasosLimite_PrecioManda_DentroDeLaTolerancia(double cantidad, double precio)
    {
        var calculo = Calcular(Gravado(), Linea((decimal)cantidad, (decimal)precio));

        VerificarTolerancias(calculo);
        Assert.Equal(Redondeo.Monto((decimal)cantidad * (decimal)precio), calculo.ImporteTotal);
    }

    [Fact]
    public void QuinientasLineas_TotalExactoYDentroDeLaTolerancia()
    {
        var azar = new Random(2026);
        var lineas = Enumerable.Range(0, 500)
            .Select(_ => Linea(Math.Round((decimal)azar.NextDouble() * 20, 3) + 0.001m, Math.Round((decimal)azar.NextDouble() * 50, 2) + 0.10m))
            .ToArray();

        var calculo = Calcular(Gravado(), lineas);

        VerificarTolerancias(calculo);
        Assert.Equal(lineas.Sum(l => Redondeo.Monto(l.Cantidad * l.ImporteUnitario)), calculo.ImporteTotal);
    }

    /// <summary>Tolerancias de ±1 de las reglas de SUNAT (3291, 3277, 3278, 3279, 3280) y cuadre interno.</summary>
    private static void VerificarTolerancias(CalculoComprobante calculo)
    {
        var t = calculo.TasaGravada / 100m;
        foreach (var linea in calculo.Lineas)
        {
            Assert.True(Math.Abs(linea.Igv - (linea.ValorVenta * linea.Tasa / 100m)) <= 1m, $"IGV de la línea {linea.Numero}");
            Assert.True(Math.Abs((linea.Linea.Cantidad * linea.ValorUnitario) - linea.ValorBruto) <= 1m, $"Valor unitario de la línea {linea.Numero}");
        }

        var gravada = calculo.Subtotales.Single(s => s.Tributo == TributoSunat.Igv);
        Assert.Equal(calculo.Lineas.Sum(l => l.ValorVenta), gravada.Base);
        Assert.Equal(calculo.Lineas.Sum(l => l.Igv), gravada.Monto);
        Assert.True(Math.Abs(gravada.Monto - (gravada.Base * t)) <= 1m, "IGV total");
        Assert.Equal(calculo.ValorVenta + gravada.Monto, calculo.PrecioVenta);
        Assert.Equal(calculo.PrecioVenta, calculo.ImporteTotal);
    }

    private static EmisorCpe Gravado(bool mype = false) => Ejemplos.Emisor(amazonia: false, mype: mype);

    private static LineaCpe Linea(decimal cantidad, decimal importe) => new() { Descripcion = "PRODUCTO", Cantidad = cantidad, ImporteUnitario = importe };

    private static ComprobanteCpe Comprobante(params LineaCpe[] lineas) => Ejemplos.Factura() with { Lineas = lineas, FormaPago = FormaPagoCpe.Contado };

    private CalculoComprobante Calcular(EmisorCpe emisor, LineaCpe linea, ModoPrecio modo) =>
        Calcular(emisor, Comprobante(linea) with { ModoPrecio = modo });

    private CalculoComprobante Calcular(EmisorCpe emisor, params LineaCpe[] lineas) => Calcular(emisor, Comprobante(lineas));

    private CalculoComprobante Calcular(EmisorCpe emisor, DateOnly fecha, params LineaCpe[] lineas) =>
        Calcular(emisor, Comprobante(lineas) with { FechaEmision = fecha });

    private CalculoComprobante Calcular(EmisorCpe emisor, ComprobanteCpe cpe) => _servicios.Obtener<CalculadoraTributaria>().Calcular(emisor, cpe);
}
