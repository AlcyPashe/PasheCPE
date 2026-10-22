namespace PasheCPE;

/// <summary>Adquiriente o usuario del comprobante.</summary>
public sealed record ClienteCpe
{
    /// <summary>Tipo de documento de identidad (catálogo N.° 06): <see cref="TipoDocumentoIdentidad"/>.</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Número de documento de identidad.</summary>
    public required string NumeroDocumento { get; init; }

    /// <summary>Apellidos y nombres, denominación o razón social.</summary>
    public required string RazonSocial { get; init; }

    /// <summary>Dirección del cliente.</summary>
    public DireccionCpe? Direccion { get; init; }

    /// <summary>Correo electrónico del cliente.</summary>
    public string? Correo { get; init; }
}

/// <summary>Forma de pago (R.S. 000193-2020): contado, o crédito con cuotas.</summary>
public sealed record FormaPagoCpe
{
    /// <summary>Pago al contado.</summary>
    public static FormaPagoCpe Contado { get; } = new() { Tipo = TipoFormaPago.Contado };

    /// <summary>Contado o crédito.</summary>
    public required TipoFormaPago Tipo { get; init; }

    /// <summary>Cuotas del crédito (Cuota001, Cuota002…).</summary>
    public IReadOnlyList<CuotaCpe> Cuotas { get; init; } = [];
}

/// <summary>Tipo de forma de pago.</summary>
public enum TipoFormaPago
{
    /// <summary>Al contado.</summary>
    Contado,

    /// <summary>Al crédito, con cuotas.</summary>
    Credito,
}

/// <summary>Cuota de un pago al crédito.</summary>
public sealed record CuotaCpe
{
    /// <summary>Monto de la cuota.</summary>
    public required decimal Monto { get; init; }

    /// <summary>Fecha de vencimiento de la cuota.</summary>
    public required DateOnly FechaVencimiento { get; init; }
}

/// <summary>Descuento o cargo global o de línea (catálogo N.° 53).</summary>
public sealed record DescuentoCargoCpe
{
    /// <summary>Código del catálogo N.° 53: <see cref="CargoDescuento"/>.</summary>
    public required string Codigo { get; init; }

    /// <summary>true si es un cargo; false si es un descuento.</summary>
    public bool EsCargo { get; init; }

    /// <summary>Monto; si es null se calcula con <see cref="Factor"/> y la base.</summary>
    public decimal? Monto { get; init; }

    /// <summary>Factor (porcentaje expresado como fracción: 0.10 = 10 %).</summary>
    public decimal? Factor { get; init; }

    /// <summary>Base sobre la que se aplica el factor; si es null la calcula la librería.</summary>
    public decimal? Base { get; init; }
}

/// <summary>Anticipo deducido en el comprobante final.</summary>
public sealed record AnticipoCpe
{
    /// <summary>Tipo del comprobante de anticipo (catálogo N.° 12): factura o boleta por anticipos.</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Serie y número del comprobante de anticipo (F001-123).</summary>
    public required string SerieNumero { get; init; }

    /// <summary>Monto del anticipo, con impuestos.</summary>
    public required decimal Monto { get; init; }

    /// <summary>Fecha de pago del anticipo.</summary>
    public DateOnly? FechaPago { get; init; }
}

/// <summary>Detracción del Sistema de Pago de Obligaciones Tributarias (SPOT).</summary>
public sealed record DetraccionCpe
{
    /// <summary>Bien o servicio sujeto (catálogo N.° 54): <see cref="BienServicioDetraccion"/>.</summary>
    public required string CodigoBienServicio { get; init; }

    /// <summary>Porcentaje de la detracción (p. ej. 12).</summary>
    public required decimal Porcentaje { get; init; }

    /// <summary>Monto; si es null se calcula con el porcentaje sobre el importe total.</summary>
    public decimal? Monto { get; init; }

    /// <summary>Número de cuenta en el Banco de la Nación.</summary>
    public required string CuentaBancoNacion { get; init; }

    /// <summary>Medio de pago (catálogo N.° 59).</summary>
    public string MedioPago { get; init; } = global::PasheCPE.MedioPago.DepositoEnCuenta;
}

/// <summary>Documento relacionado al comprobante.</summary>
public sealed record DocumentoRelacionadoCpe
{
    /// <summary>Tipo de documento (catálogo N.° 12 o N.° 01).</summary>
    public required string Tipo { get; init; }

    /// <summary>Número del documento.</summary>
    public required string Numero { get; init; }
}

/// <summary>Contrato de colaboración empresarial (vigente desde el 01/01/2027).</summary>
public sealed record ContratoColaboracionCpe
{
    /// <summary>Tipo de contrato: «1» o «2».</summary>
    public required string Tipo { get; init; }

    /// <summary>Número del contrato (1 a 50 caracteres alfanuméricos).</summary>
    public required string Numero { get; init; }

    /// <summary>Descripción (1 a 250 caracteres).</summary>
    public required string Descripcion { get; init; }

    /// <summary>Porcentaje de participación (2 enteros y 2 decimales).</summary>
    public required decimal Porcentaje { get; init; }
}

/// <summary>Leyenda del comprobante (catálogo N.° 52).</summary>
public sealed record LeyendaCpe
{
    /// <summary>Código del catálogo N.° 52: <see cref="CodigoLeyenda"/>.</summary>
    public required string Codigo { get; init; }

    /// <summary>Texto de la leyenda.</summary>
    public required string Texto { get; init; }
}
