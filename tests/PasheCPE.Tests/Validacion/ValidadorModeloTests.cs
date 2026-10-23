using PasheCPE.Tests.Utilidades;
using PasheCPE.Tributos;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Validacion;

public sealed class ValidadorModeloTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    [Fact]
    public void Validar_FacturaDeEjemplo_NoTieneErrores() =>
        Assert.Empty(Validar(Ejemplos.Emisor(), Ejemplos.Factura()));

    [Fact]
    public void Validar_BoletaAnonimaPequena_NoTieneErrores() =>
        Assert.Empty(Validar(Ejemplos.Emisor(), Boleta(100m)));

    [Theory]
    [InlineData("RucEmisor", "PCPE-1201", null)]
    [InlineData("TipoDocumento", "PCPE-1203", null)]
    [InlineData("SerieFactura", "PCPE-1204", null)]
    [InlineData("Numero", "PCPE-1205", null)]
    [InlineData("FechaFutura", "PCPE-1206", null)]
    [InlineData("PlazoVencido", "PCPE-1207", null)]
    [InlineData("Vencimiento", "PCPE-1208", null)]
    [InlineData("OperacionDeBoleta", "PCPE-1209", null)]
    [InlineData("OperacionInexistente", "PCPE-1001", "51")]
    [InlineData("Moneda", "PCPE-1003", "02")]
    [InlineData("ClienteDni", "PCPE-1210", null)]
    [InlineData("Cantidad", "PCPE-1220", null)]
    [InlineData("ImporteNegativo", "PCPE-1221", null)]
    [InlineData("Descripcion", "PCPE-1222", null)]
    [InlineData("CodigoProducto", "PCPE-1223", null)]
    [InlineData("Gtin", "PCPE-1224", null)]
    [InlineData("GratuitoIncoherente", "PCPE-1225", null)]
    [InlineData("Ivap", "PCPE-1226", null)]
    [InlineData("IscSistema03", "PCPE-1227", null)]
    [InlineData("DescuentoExcesivo", "PCPE-1228", null)]
    [InlineData("AfectacionInexistente", "PCPE-1001", "07")]
    [InlineData("Unidad", "PCPE-1003", "03")]
    [InlineData("CreditoSinCuotas", "PCPE-1230", null)]
    [InlineData("CuotaVencida", "PCPE-1232", null)]
    [InlineData("ContadoConCuotas", "PCPE-1233", null)]
    [InlineData("DetraccionOperacion", "PCPE-1240", null)]
    [InlineData("DetraccionCuenta", "PCPE-1241", null)]
    [InlineData("DetraccionDolares", "PCPE-1242", null)]
    [InlineData("DetraccionBien", "PCPE-1001", "54")]
    [InlineData("ContratoAntesDe2027", "PCPE-1250", null)]
    [InlineData("DescuentoNoSoportado", "PCPE-1260", null)]
    [InlineData("DescuentoIncoherente", "PCPE-1262", null)]
    [InlineData("Anticipo", "PCPE-1270", null)]
    [InlineData("Leyenda", "PCPE-1001", "52")]
    public void Validar_Factura_DetectaElError(string caso, string codigo, string? catalogo)
    {
        var emisor = caso == "RucEmisor" ? Ejemplos.Emisor() with { Ruc = "20123456789" } : Ejemplos.Emisor();
        var factura = Modificar(Ejemplos.Factura(), caso);

        var errores = Validar(emisor, factura);

        var error = Assert.Single(errores, e => e.Codigo == codigo);
        Assert.Equal(OrigenError.Modelo, error.Origen);
        Assert.Equal(catalogo, error.Catalogo);
    }

    [Fact]
    public void Validar_SerieDeFacturaEnBoleta_DevuelveError() =>
        Assert.Contains(Validar(Ejemplos.Emisor(), Boleta(10m) with { Serie = "F001" }), e => e.Codigo == "PCPE-1204");

    [Fact]
    public void Validar_DniMalFormadoEnBoleta_DevuelveError() =>
        Assert.Contains(
            Validar(Ejemplos.Emisor(), Boleta(10m) with { Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "123", RazonSocial = "JUAN PÉREZ" } }),
            e => e.Codigo == "PCPE-1212");

    [Fact]
    public void Validar_FacturaDeEmisorNrus_DevuelveError() =>
        Assert.Contains(Validar(Ejemplos.Emisor(RegimenTributario.NRUS), Ejemplos.Factura()), e => e.Codigo == "PCPE-1101");

    [Fact]
    public void Validar_GratuitoSinAfectacionEnEmisorGravado_PideLaAfectacion()
    {
        var factura = Ejemplos.Factura() with { Lineas = [Ejemplos.Factura().Lineas[0] with { EsGratuito = true }] };

        Assert.Contains(Validar(Ejemplos.Emisor(amazonia: false), factura), e => e.Codigo == "PCPE-1229");
    }

    [Fact]
    public void Validar_ContratoDeColaboracionEn2027_Valida()
    {
        using var servicios2027 = new ServiciosPrueba(ahora: new DateTimeOffset(2027, 1, 5, 15, 0, 0, TimeSpan.Zero));
        var factura = Ejemplos.Factura() with
        {
            FechaEmision = new DateOnly(2027, 1, 5),
            FormaPago = FormaPagoCpe.Contado,
            ContratoColaboracion = new ContratoColaboracionCpe { Tipo = "1", Numero = "CCE2027", Descripcion = "Consorcio", Porcentaje = 45.50m },
        };
        var invalido = factura with { ContratoColaboracion = factura.ContratoColaboracion with { Tipo = "3" } };

        var validador = servicios2027.Obtener<ValidadorModelo>();
        Assert.Empty(validador.Validar(Ejemplos.Emisor(), factura));
        Assert.Contains(validador.Validar(Ejemplos.Emisor(), invalido), e => e.Codigo == "PCPE-1251");
    }

    [Fact]
    public void ValidarTotales_BoletaAnonimaMayorA700_PideIdentificacion()
    {
        var calculo = Calcular(Ejemplos.Emisor(), Boleta(700.01m));

        Assert.Equal("PCPE-1211", Assert.Single(_servicios.Obtener<ValidadorModelo>().ValidarTotales(calculo)).Codigo);
    }

    [Fact]
    public void ValidarTotales_BoletaAnonimaDe700_NoPideIdentificacion() =>
        Assert.Empty(_servicios.Obtener<ValidadorModelo>().ValidarTotales(Calcular(Ejemplos.Emisor(), Boleta(700m))));

    [Fact]
    public void ValidarTotales_CuotasQueNoSuman_DevuelveError()
    {
        var factura = Ejemplos.Factura() with
        {
            FormaPago = new FormaPagoCpe { Tipo = TipoFormaPago.Credito, Cuotas = [new CuotaCpe { Monto = 30m, FechaVencimiento = new DateOnly(2026, 11, 22) }] },
        };

        Assert.Equal("PCPE-1231", Assert.Single(_servicios.Obtener<ValidadorModelo>().ValidarTotales(Calcular(Ejemplos.Emisor(), factura))).Codigo);
    }

    [Fact]
    public void ValidarTotales_DescuentoQueAfectaLaBaseSinGravadas_DevuelveError()
    {
        var factura = Ejemplos.Factura() with
        {
            Lineas = [Ejemplos.Factura().Lineas[0]],
            FormaPago = FormaPagoCpe.Contado,
            DescuentosCargos = [new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoGlobalAfectaBase, Monto = 5m }],
        };

        Assert.Equal("PCPE-1261", Assert.Single(_servicios.Obtener<ValidadorModelo>().ValidarTotales(Calcular(Ejemplos.Emisor(), factura))).Codigo);
    }

    [Theory]
    [InlineData("20611619767", true)]
    [InlineData("20123456786", true)]
    [InlineData("20123456789", false)]
    [InlineData("2012345678", false)]
    [InlineData("2012345678A", false)]
    public void Ruc_DigitoVerificador(string ruc, bool valido) => Assert.Equal(valido, DocumentosIdentidad.EsRucValido(ruc));

    private static ComprobanteCpe Modificar(ComprobanteCpe f, string caso)
    {
        var linea = f.Lineas[0];
        ComprobanteCpe ConLinea(LineaCpe l) => f with { Lineas = [l], FormaPago = FormaPagoCpe.Contado };
        var detraccion = new DetraccionCpe { CodigoBienServicio = "037", Porcentaje = 12m, CuentaBancoNacion = "00-000-123456" };
        return caso switch
        {
            "RucEmisor" => f,
            "TipoDocumento" => f with { TipoDocumento = TipoDocumentoSunat.NotaDeCredito },
            "SerieFactura" => f with { Serie = "B001" },
            "Numero" => f with { Numero = 0 },
            "FechaFutura" => f with { FechaEmision = new DateOnly(2026, 10, 23) },
            "PlazoVencido" => f with { FechaEmision = new DateOnly(2026, 10, 18), FormaPago = FormaPagoCpe.Contado },
            "Vencimiento" => f with { FechaVencimiento = new DateOnly(2026, 10, 1) },
            "OperacionDeBoleta" => f with { TipoOperacion = TipoOperacion.VentaInternaNrus },
            "OperacionInexistente" => f with { TipoOperacion = "9999" },
            "Moneda" => f with { Moneda = "PE" },
            "ClienteDni" => f with { Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "JUAN PÉREZ" } },
            "Cantidad" => ConLinea(linea with { Cantidad = 0 }),
            "ImporteNegativo" => ConLinea(linea with { ImporteUnitario = -1 }),
            "Descripcion" => ConLinea(linea with { Descripcion = " " }),
            "CodigoProducto" => ConLinea(linea with { CodigoProductoSunat = "00000000" }),
            "Gtin" => ConLinea(linea with { Gtin = "123" }),
            "GratuitoIncoherente" => ConLinea(linea with { EsGratuito = true, Afectacion = AfectacionIgv.GravadoOneroso }),
            "Ivap" => ConLinea(linea with { Afectacion = AfectacionIgv.GravadoIvap }),
            "IscSistema03" => ConLinea(linea with { Isc = new IscLineaCpe { Sistema = SistemaIsc.PreciosVentaAlPublico, Tasa = 10m } }),
            "DescuentoExcesivo" => ConLinea(linea with { Descuento = 1000m }),
            "AfectacionInexistente" => ConLinea(linea with { Afectacion = "99" }),
            "Unidad" => ConLinea(linea with { Unidad = "niu" }),
            "CreditoSinCuotas" => f with { FormaPago = new FormaPagoCpe { Tipo = TipoFormaPago.Credito } },
            "CuotaVencida" => f with { FormaPago = new FormaPagoCpe { Tipo = TipoFormaPago.Credito, Cuotas = [new CuotaCpe { Monto = 61m, FechaVencimiento = new DateOnly(2026, 10, 22) }] } },
            "ContadoConCuotas" => f with { FormaPago = new FormaPagoCpe { Tipo = TipoFormaPago.Contado, Cuotas = [new CuotaCpe { Monto = 61m, FechaVencimiento = new DateOnly(2026, 11, 22) }] } },
            "DetraccionOperacion" => f with { Detraccion = detraccion },
            "DetraccionCuenta" => f with { TipoOperacion = "1001", Detraccion = detraccion with { CuentaBancoNacion = "" } },
            "DetraccionDolares" => f with { TipoOperacion = "1001", Moneda = "USD", Detraccion = detraccion },
            "DetraccionBien" => f with { TipoOperacion = "1001", Detraccion = detraccion with { CodigoBienServicio = "999" } },
            "ContratoAntesDe2027" => f with { ContratoColaboracion = new ContratoColaboracionCpe { Tipo = "1", Numero = "C1", Descripcion = "Consorcio", Porcentaje = 50m } },
            "DescuentoNoSoportado" => f with { DescuentosCargos = [new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoLineaNoAfectaBase, Monto = 1m }] },
            "DescuentoIncoherente" => f with { DescuentosCargos = [new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoGlobalAfectaBase, EsCargo = true, Monto = 1m }] },
            "Anticipo" => f with { Anticipos = [new AnticipoCpe { TipoDocumento = TipoDocumentoSunat.Factura, SerieNumero = "F001-1", Monto = 10m }] },
            "Leyenda" => f with { LeyendasAdicionales = [new LeyendaCpe { Codigo = "9999", Texto = "X" }] },
            _ => throw new ArgumentOutOfRangeException(nameof(caso), caso, null),
        };
    }

    private static ComprobanteCpe Boleta(decimal importe) => new()
    {
        TipoDocumento = TipoDocumentoSunat.BoletaDeVenta,
        Serie = "B001",
        Numero = 45,
        FechaEmision = new DateOnly(2026, 10, 22),
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.NoDomiciliadoSinRuc, NumeroDocumento = "-", RazonSocial = "CLIENTES VARIOS" },
        Lineas = [new LineaCpe { Descripcion = "VENTA", Cantidad = 1, ImporteUnitario = importe }],
    };

    private IReadOnlyList<ErrorCpe> Validar(EmisorCpe emisor, ComprobanteCpe cpe) => _servicios.Obtener<ValidadorModelo>().Validar(emisor, cpe);

    private CalculoComprobante Calcular(EmisorCpe emisor, ComprobanteCpe cpe) => _servicios.Obtener<CalculadoraTributaria>().Calcular(emisor, cpe);
}
