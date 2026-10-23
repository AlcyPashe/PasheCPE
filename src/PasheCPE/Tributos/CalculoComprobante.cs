namespace PasheCPE.Tributos;

/// <summary>
/// Resultado del cálculo tributario de un comprobante o una nota: todo lo que el mapeador UBL necesita, ya redondeado.
/// </summary>
internal sealed record CalculoComprobante
{
    public required EmisorCpe Emisor { get; init; }

    /// <summary>Factura o boleta (<see cref="ComprobanteCpe"/>) o nota (<see cref="NotaCpe"/>).</summary>
    public required DocumentoVentaCpe Comprobante { get; init; }

    public required DateOnly FechaEmision { get; init; }

    public required TimeOnly HoraEmision { get; init; }

    /// <summary>Tasa del IGV aplicada a las líneas gravadas (porcentaje: 18, 10.5…).</summary>
    public required decimal TasaGravada { get; init; }

    public required IReadOnlyList<LineaCalculada> Lineas { get; init; }

    public required IReadOnlyList<SubtotalTributo> Subtotales { get; init; }

    public required IReadOnlyList<CargoCalculado> CargosGlobales { get; init; }

    public required IReadOnlyList<AnticipoCalculado> Anticipos { get; init; }

    public DetraccionCalculada? Detraccion { get; init; }

    public required IReadOnlyList<LeyendaCpe> Leyendas { get; init; }

    /// <summary>Total valor de venta (LineExtensionAmount del documento).</summary>
    public decimal ValorVenta { get; init; }

    /// <summary>Total precio de venta (TaxInclusiveAmount).</summary>
    public decimal PrecioVenta { get; init; }

    /// <summary>Descuentos que no afectan la base (AllowanceTotalAmount).</summary>
    public decimal DescuentosNoBase { get; init; }

    /// <summary>Cargos que no afectan la base (ChargeTotalAmount).</summary>
    public decimal CargosNoBase { get; init; }

    /// <summary>Total de anticipos (PrepaidAmount).</summary>
    public decimal TotalAnticipos { get; init; }

    /// <summary>Importe total a pagar (PayableAmount).</summary>
    public decimal ImporteTotal { get; init; }

    /// <summary>Suma de tributos del documento (TaxTotal/TaxAmount): IGV, ISC e ICBPER, sin gratuitas.</summary>
    public decimal TotalTributos { get; init; }

    /// <summary>Importe a pagar menos la detracción: monto neto pendiente del crédito.</summary>
    public decimal MontoNetoPendiente { get; init; }

    public IReadOnlyList<ObservacionCpe> Advertencias { get; init; } = [];

    /// <summary>Errores del cálculo (p. ej. anticipos mayores que el total).</summary>
    public IReadOnlyList<ErrorCpe> Errores { get; init; } = [];
}

/// <summary>Línea con sus importes calculados.</summary>
internal sealed record LineaCalculada
{
    public required int Numero { get; init; }

    public required LineaCpe Linea { get; init; }

    /// <summary>Afectación del IGV (catálogo N.° 07).</summary>
    public required string Afectacion { get; init; }

    /// <summary>Tributo de la línea (catálogo N.° 05): 1000, 9995, 9996, 9997 o 9998.</summary>
    public required string Tributo { get; init; }

    public bool EsGratuita { get; init; }

    /// <summary>Tasa del IGV de la línea en porcentaje (0 si no es gravada).</summary>
    public decimal Tasa { get; init; }

    /// <summary>Valor de venta antes del descuento de línea.</summary>
    public decimal ValorBruto { get; init; }

    public decimal Descuento { get; init; }

    /// <summary>Valor de venta de la línea (LineExtensionAmount): bruto menos descuento.</summary>
    public decimal ValorVenta { get; init; }

    public decimal Isc { get; init; }

    public decimal Igv { get; init; }

    public decimal Icbper { get; init; }

    /// <summary>Tarifa del ICBPER por bolsa aplicada.</summary>
    public decimal TarifaIcbper { get; init; }

    /// <summary>Valor unitario sin impuestos (cac:Price): bruto ÷ cantidad, hasta 10 decimales; 0 en gratuitas.</summary>
    public decimal ValorUnitario { get; init; }

    /// <summary>Precio de referencia (cac:PricingReference): con impuestos (tipo 01) o valor referencial (tipo 02).</summary>
    public decimal PrecioReferencia { get; init; }

    /// <summary>Tipo de precio (catálogo N.° 16).</summary>
    public required string TipoPrecio { get; init; }

    /// <summary>Total de tributos de la línea (TaxTotal/TaxAmount).</summary>
    public decimal TotalTributos => Igv + Isc + Icbper;
}

/// <summary>Total por tributo del documento (cac:TaxTotal/cac:TaxSubtotal).</summary>
internal sealed record SubtotalTributo(string Tributo, decimal Base, decimal Monto);

/// <summary>Descuento o cargo global, o deducción de un anticipo (cac:AllowanceCharge).</summary>
internal sealed record CargoCalculado(string Codigo, bool EsCargo, decimal Factor, decimal Monto, decimal Base);

/// <summary>Anticipo deducido (cac:PrepaidPayment + cac:AdditionalDocumentReference).</summary>
internal sealed record AnticipoCalculado(int Identificador, AnticipoCpe Anticipo, string Codigo, decimal Deduccion);

/// <summary>Detracción con su monto en soles.</summary>
internal sealed record DetraccionCalculada(DetraccionCpe Detraccion, decimal Monto);
