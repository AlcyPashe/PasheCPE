namespace PasheCPE;

/// <summary>
/// Datos comunes de los comprobantes (01, 03) y de las notas (07, 08). El sistema de ventas entrega cantidades,
/// precios y afectaciones; la librería calcula bases, impuestos, totales y leyendas (PRD RF-03).
/// </summary>
public abstract record DocumentoVentaCpe
{
    /// <summary>Tipo de documento (catálogo N.° 01): <see cref="TipoDocumentoSunat"/>.</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Serie de 4 caracteres (F001, B001, FC01…).</summary>
    public required string Serie { get; init; }

    /// <summary>Número correlativo (sin ceros a la izquierda).</summary>
    public required long Numero { get; init; }

    /// <summary>Fecha de emisión; si es null se usa la fecha actual de Lima.</summary>
    public DateOnly? FechaEmision { get; init; }

    /// <summary>Hora de emisión; si es null se usa la hora actual de Lima.</summary>
    public TimeOnly? HoraEmision { get; init; }

    /// <summary>Moneda ISO 4217 (catálogo N.° 02).</summary>
    public string Moneda { get; init; } = "PEN";

    /// <summary>Tipo de operación (catálogo N.° 51): <see cref="PasheCPE.TipoOperacion"/>.</summary>
    public string TipoOperacion { get; init; } = global::PasheCPE.TipoOperacion.VentaInterna;

    /// <summary>Indica si <see cref="LineaCpe.ImporteUnitario"/> incluye impuestos.</summary>
    public ModoPrecio ModoPrecio { get; init; } = ModoPrecio.PrecioUnitario;

    /// <summary>Adquiriente o usuario.</summary>
    public required ClienteCpe Cliente { get; init; }

    /// <summary>Detalle del documento.</summary>
    public required IReadOnlyList<LineaCpe> Lineas { get; init; }

    /// <summary>Forma de pago; si es null se considera al contado.</summary>
    public FormaPagoCpe? FormaPago { get; init; }

    /// <summary>Descuentos y cargos globales (catálogo N.° 53).</summary>
    public IReadOnlyList<DescuentoCargoCpe> DescuentosCargos { get; init; } = [];

    /// <summary>Documentos relacionados (catálogo N.° 12).</summary>
    public IReadOnlyList<DocumentoRelacionadoCpe> DocumentosRelacionados { get; init; } = [];

    /// <summary>Número de la orden de compra del cliente.</summary>
    public string? OrdenCompra { get; init; }

    /// <summary>Contrato de colaboración empresarial (campo opcional vigente desde el 01/01/2027).</summary>
    public ContratoColaboracionCpe? ContratoColaboracion { get; init; }

    /// <summary>Leyendas adicionales (catálogo N.° 52); el monto en letras y la leyenda Amazonía las agrega la librería.</summary>
    public IReadOnlyList<LeyendaCpe> LeyendasAdicionales { get; init; } = [];

    /// <summary>Observaciones libres para la representación impresa.</summary>
    public string? Observaciones { get; init; }
}

/// <summary>Factura (01) o boleta de venta (03).</summary>
public sealed record ComprobanteCpe : DocumentoVentaCpe
{
    /// <summary>Fecha de vencimiento (o de la última cuota si es al crédito).</summary>
    public DateOnly? FechaVencimiento { get; init; }

    /// <summary>Anticipos deducidos en el comprobante.</summary>
    public IReadOnlyList<AnticipoCpe> Anticipos { get; init; } = [];

    /// <summary>Detracción (SPOT), si la operación está sujeta.</summary>
    public DetraccionCpe? Detraccion { get; init; }
}

/// <summary>Nota de crédito (07) o de débito (08). Modifica un único comprobante (R.S. 000048-2026).</summary>
public sealed record NotaCpe : DocumentoVentaCpe
{
    /// <summary>Motivo: catálogo N.° 09 (<see cref="MotivoNotaCredito"/>) o N.° 10 (<see cref="MotivoNotaDebito"/>).</summary>
    public required string Motivo { get; init; }

    /// <summary>Sustento o descripción del motivo.</summary>
    public required string Sustento { get; init; }

    /// <summary>El comprobante que modifica la nota.</summary>
    public required DocumentoModificadoCpe DocumentoModificado { get; init; }
}

/// <summary>Comprobante que modifica una nota de crédito o débito.</summary>
public sealed record DocumentoModificadoCpe
{
    /// <summary>Tipo de documento modificado (catálogo N.° 01).</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Serie del documento modificado.</summary>
    public required string Serie { get; init; }

    /// <summary>Número del documento modificado.</summary>
    public required long Numero { get; init; }
}

/// <summary>Cómo interpretar el importe unitario de las líneas.</summary>
public enum ModoPrecio
{
    /// <summary>El importe unitario incluye impuestos (precio de venta); la librería obtiene el valor unitario.</summary>
    PrecioUnitario,

    /// <summary>El importe unitario no incluye impuestos (valor unitario).</summary>
    ValorUnitario,
}
