namespace PasheCPE;

/// <summary>Línea de detalle de un comprobante o nota.</summary>
public sealed record LineaCpe
{
    /// <summary>Código interno del producto en el sistema de ventas.</summary>
    public string? CodigoInterno { get; init; }

    /// <summary>Código de producto SUNAT de 8 dígitos (UNSPSC, catálogo N.° 25).</summary>
    public string? CodigoProductoSunat { get; init; }

    /// <summary>Código GTIN (8, 12, 13 o 14 dígitos).</summary>
    public string? Gtin { get; init; }

    /// <summary>Descripción del bien o servicio.</summary>
    public required string Descripcion { get; init; }

    /// <summary>Unidad de medida UN/ECE Rec. 20 (catálogo N.° 03): <see cref="UnidadMedida"/>.</summary>
    public string Unidad { get; init; } = UnidadMedida.Unidad;

    /// <summary>Cantidad.</summary>
    public required decimal Cantidad { get; init; }

    /// <summary>Importe unitario: precio con impuestos o valor sin impuestos, según <see cref="DocumentoVentaCpe.ModoPrecio"/>.</summary>
    public required decimal ImporteUnitario { get; init; }

    /// <summary>Afectación del IGV (catálogo N.° 07); si es null se usa la del emisor (Amazonía → 20, si no → 10).</summary>
    public string? Afectacion { get; init; }

    /// <summary>Descuento de la línea, sin impuestos.</summary>
    public decimal? Descuento { get; init; }

    /// <summary>Cantidad de bolsas plásticas afectas al ICBPER.</summary>
    public int BolsasIcbper { get; init; }

    /// <summary>Transferencia gratuita (el importe unitario se informa como valor referencial).</summary>
    public bool EsGratuito { get; init; }

    /// <summary>Impuesto Selectivo al Consumo, si aplica.</summary>
    public IscLineaCpe? Isc { get; init; }

    /// <summary>Naturaleza del ítem; define la leyenda de la Amazonía (2001, 2002 o 2003).</summary>
    public TipoItem TipoItem { get; init; } = TipoItem.Bien;
}

/// <summary>Naturaleza del ítem.</summary>
public enum TipoItem
{
    /// <summary>Bien.</summary>
    Bien,

    /// <summary>Servicio.</summary>
    Servicio,

    /// <summary>Contrato de construcción.</summary>
    Construccion,
}

/// <summary>ISC de una línea.</summary>
public sealed record IscLineaCpe
{
    /// <summary>Sistema de cálculo (catálogo N.° 08): <see cref="SistemaIsc"/>.</summary>
    public required string Sistema { get; init; }

    /// <summary>Tasa del ISC (porcentaje), para el sistema al valor.</summary>
    public decimal? Tasa { get; init; }

    /// <summary>Monto fijo por unidad, para el sistema específico.</summary>
    public decimal? MontoPorUnidad { get; init; }
}
