namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// Comprobantes de ejemplo que cubren los casos tributarios de la Fase 2. Se usan para comprobar que el XML generado
/// pasa las XSL de SUNAT y las reglas complementarias sin errores ni observaciones.
/// </summary>
internal static class CasosTributarios
{
    private static readonly DateOnly _fecha = new(2026, 10, 22);

    public static IReadOnlyDictionary<string, CasoTributario> Todos { get; } = new Dictionary<string, CasoTributario>(StringComparer.Ordinal)
    {
        ["Gravada18"] = new(Gravado(), Factura(Linea(2, 59m), Linea(1, 11.80m))),
        ["Gravada18ValorUnitario"] = new(Gravado(), Factura(Linea(3, 10m)) with { ModoPrecio = ModoPrecio.ValorUnitario }),
        ["ExoneradaAmazonia"] = new(Ejemplos.Emisor(), Ejemplos.Factura()),
        ["MypeDiezYMedio2026"] = new(Gravado(mype: true), Factura(Linea(1, 110.50m), Linea(3, 22.10m))),
        ["MypeQuince2027"] = new(Gravado(mype: true), Factura(Linea(1, 115m)) with { FechaEmision = new DateOnly(2027, 6, 15) }, new DateOnly(2027, 6, 15)),
        ["Mixta"] = new(Gravado(), Factura(Linea(1, 118m), Linea(2, 25m) with { Afectacion = AfectacionIgv.ExoneradoOneroso }, Linea(1, 40m) with { Afectacion = AfectacionIgv.InafectoOneroso })),
        ["GratuitaGravada"] = new(Gravado(), Factura(Linea(1, 118m), Linea(2, 11.80m) with { Afectacion = AfectacionIgv.GravadoBonificaciones, EsGratuito = true })),
        ["GratuitaExonerada"] = new(Ejemplos.Emisor(), Factura(Linea(1, 50m), Linea(20, 1.96m) with { EsGratuito = true })),
        ["Icbper"] = new(Gravado(), Factura(Linea(1, 11.80m), Linea(2, 0.20m) with { Descripcion = "BOLSA PLÁSTICA", BolsasIcbper = 2 })),
        ["IscAlValor"] = new(Gravado(), Factura(Linea(1, 100m) with { Isc = new IscLineaCpe { Sistema = SistemaIsc.SistemaAlValor, Tasa = 10m } }) with { ModoPrecio = ModoPrecio.ValorUnitario }),
        ["IscMontoFijo"] = new(Gravado(), Factura(Linea(2, 50m) with { Isc = new IscLineaCpe { Sistema = SistemaIsc.MontoFijo, MontoPorUnidad = 1.5m } }) with { ModoPrecio = ModoPrecio.ValorUnitario }),
        ["CreditoTresCuotas"] = new(Gravado(), Factura(Linea(1, 354m)) with
        {
            FormaPago = new FormaPagoCpe
            {
                Tipo = TipoFormaPago.Credito,
                Cuotas =
                [
                    new CuotaCpe { Monto = 118m, FechaVencimiento = new DateOnly(2026, 11, 22) },
                    new CuotaCpe { Monto = 118m, FechaVencimiento = new DateOnly(2026, 12, 22) },
                    new CuotaCpe { Monto = 118m, FechaVencimiento = new DateOnly(2027, 1, 22) },
                ],
            },
        }),
        ["Detraccion"] = new(Gravado(), Factura(Linea(1, 1180m)) with
        {
            TipoOperacion = "1001",
            Detraccion = new DetraccionCpe { CodigoBienServicio = "037", Porcentaje = 12m, CuentaBancoNacion = "00-000-123456" },
        }),
        ["AnticipoGravado"] = new(Gravado(), Factura(Linea(1, 1180m)) with
        {
            Anticipos = [new AnticipoCpe { TipoDocumento = DocumentoRelacionadoTributario.FacturaAnticipos, SerieNumero = "F001-1", Monto = 118m, Afectacion = AfectacionIgv.GravadoOneroso }],
        }),
        ["AnticipoExonerado"] = new(Ejemplos.Emisor(), Factura(Linea(1, 500m)) with
        {
            Anticipos = [new AnticipoCpe { TipoDocumento = DocumentoRelacionadoTributario.FacturaAnticipos, SerieNumero = "F001-1", Monto = 100m }],
        }),
        ["DescuentoGlobalAfectaBase"] = new(Gravado(), Factura(Linea(1, 1180m)) with
        {
            DescuentosCargos = [new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoGlobalAfectaBase, Factor = 0.10m }],
        }),
        ["DescuentosYCargosNoBase"] = new(Gravado(), Factura(Linea(1, 118m)) with
        {
            DescuentosCargos =
            [
                new DescuentoCargoCpe { Codigo = CargoDescuento.DescuentoGlobalNoAfectaBase, Monto = 10m },
                new DescuentoCargoCpe { Codigo = CargoDescuento.CargoGlobalNoAfectaBase, EsCargo = true, Monto = 5m },
                new DescuentoCargoCpe { Codigo = CargoDescuento.CargoGlobalAfectaBase, EsCargo = true, Monto = 50m },
            ],
        }),
        ["DescuentoDeLinea"] = new(Gravado(), Factura(Linea(2, 59m) with { Descuento = 10m })),
        ["Exportacion"] = new(Gravado(), Factura(Linea(1, 300m) with { Afectacion = AfectacionIgv.Exportacion }) with
        {
            TipoOperacion = TipoOperacion.ExportacionBienes,
            Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.NoDomiciliadoSinRuc, NumeroDocumento = "-", RazonSocial = "CLIENTE DEL EXTERIOR" },
        }),
        ["ContratoColaboracion2027"] = new(Gravado(), Factura(Linea(1, 118m)) with
        {
            FechaEmision = new DateOnly(2027, 1, 15),
            ContratoColaboracion = new ContratoColaboracionCpe { Tipo = "1", Numero = "CCE-2027-001", Descripcion = "CONSORCIO VIAL DE PRUEBA", Porcentaje = 50m },
        }, new DateOnly(2027, 1, 15)),
        ["BoletaGravada"] = new(Gravado(), Boleta(Linea(1, 59m), Linea(2, 5.90m))),
        ["BoletaMypeDiezYMedio"] = new(Gravado(mype: true), Boleta(Linea(1, 110.50m), Linea(2, 22.10m))),
        ["BoletaExonerada"] = new(Ejemplos.Emisor(), Boleta(Linea(1, 25.50m))),
        ["BoletaClienteIdentificado"] = new(Gravado(), Boleta(Linea(10, 118m)) with
        {
            Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "JUAN PÉREZ" },
        }),
    };

    public static EmisorCpe Gravado(bool mype = false) => Ejemplos.Emisor(amazonia: false, mype: mype);

    public static LineaCpe Linea(decimal cantidad, decimal importe) =>
        new() { Descripcion = "PRODUCTO", Cantidad = cantidad, ImporteUnitario = importe, CodigoProductoSunat = "50221200" };

    public static ComprobanteCpe Factura(params LineaCpe[] lineas) =>
        Ejemplos.Factura() with { Lineas = lineas, FormaPago = FormaPagoCpe.Contado };

    public static ComprobanteCpe Boleta(params LineaCpe[] lineas) =>
        Ejemplos.Factura() with
        {
            TipoDocumento = TipoDocumentoSunat.BoletaDeVenta,
            Serie = "B001",
            Lineas = lineas,
            FormaPago = FormaPagoCpe.Contado,
            Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.NoDomiciliadoSinRuc, NumeroDocumento = "-", RazonSocial = "CLIENTES VARIOS" },
        };

    public static DateTimeOffset Ahora(DateOnly fecha) => new(fecha.Year, fecha.Month, fecha.Day, 15, 0, 0, TimeSpan.Zero);
}

/// <summary>Emisor y comprobante de un caso, con la fecha de emisión (también la del reloj de la prueba).</summary>
internal sealed record CasoTributario(EmisorCpe Emisor, ComprobanteCpe Comprobante, DateOnly? Fecha = null)
{
    public DateOnly FechaEmision => Fecha ?? Comprobante.FechaEmision ?? new DateOnly(2026, 10, 22);
}
