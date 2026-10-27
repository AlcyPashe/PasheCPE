namespace PasheCPE.Pdf.Representacion;

/// <summary>Emisor, cliente, remitente, destinatario o transportista tal como figura en el XML firmado.</summary>
internal sealed record ParteImpresa(string TipoDocumento, string NumeroDocumento, string Nombre)
{
    public string? NombreComercial { get; init; }

    public string? Direccion { get; init; }

    /// <summary>Registro del MTC del transportista (GRE).</summary>
    public string? RegistroMtc { get; init; }
}

/// <summary>Estado del documento según su CDR (o el historial si no hay CDR).</summary>
internal sealed record EstadoSunatImpreso(string Texto, DateOnly? Fecha);

/// <summary>Factura, boleta, nota de crédito o nota de débito leída del XML firmado (TRD §22).</summary>
internal sealed record ComprobanteImpreso
{
    public required ReferenciaCpe Referencia { get; init; }

    public required string TipoDocumento { get; init; }

    public required string SerieNumero { get; init; }

    public required DateOnly FechaEmision { get; init; }

    public TimeOnly? HoraEmision { get; init; }

    public DateOnly? FechaVencimiento { get; init; }

    public required string Moneda { get; init; }

    public string? TipoOperacion { get; init; }

    public required ParteImpresa Emisor { get; init; }

    public required ParteImpresa Cliente { get; init; }

    /// <summary>«Contado» o «Crédito» (null si el XML no trae la forma de pago, como en las notas sin cuotas).</summary>
    public string? FormaPago { get; init; }

    public decimal? MontoPendiente { get; init; }

    public IReadOnlyList<CuotaImpresa> Cuotas { get; init; } = [];

    /// <summary>Documento que modifica la nota (tipo y serie-número).</summary>
    public DocumentoRelacionadoImpreso? DocumentoModificado { get; init; }

    public string? CodigoMotivo { get; init; }

    public string? Motivo { get; init; }

    public string? Sustento { get; init; }

    public string? OrdenCompra { get; init; }

    public IReadOnlyList<DocumentoRelacionadoImpreso> Guias { get; init; } = [];

    public IReadOnlyList<LineaImpresa> Lineas { get; init; } = [];

    public required TotalesImpresos Totales { get; init; }

    public IReadOnlyList<LeyendaImpresa> Leyendas { get; init; } = [];

    public DetraccionImpresa? Detraccion { get; init; }

    public string? ValorResumen { get; init; }

    public required string ContenidoQr { get; init; }

    public EstadoSunatImpreso? Estado { get; init; }
}

internal sealed record CuotaImpresa(string Numero, decimal Monto, DateOnly? Vencimiento);

internal sealed record DocumentoRelacionadoImpreso(string Tipo, string Numero)
{
    public string? Emisor { get; init; }
}

internal sealed record LeyendaImpresa(string Codigo, string Texto);

internal sealed record DetraccionImpresa(string? Codigo, string? Descripcion, decimal? Porcentaje, decimal? Monto, string? Cuenta, string? MedioPago);

internal sealed record LineaImpresa
{
    public required int Numero { get; init; }

    public string? Codigo { get; init; }

    public required string Descripcion { get; init; }

    public required string Unidad { get; init; }

    public required decimal Cantidad { get; init; }

    /// <summary>Valor unitario (sin impuestos): <c>cac:Price/cbc:PriceAmount</c>.</summary>
    public decimal ValorUnitario { get; init; }

    /// <summary>Precio unitario con impuestos o valor referencial si es gratuita (<c>AlternativeConditionPrice</c>).</summary>
    public decimal PrecioUnitario { get; init; }

    public decimal Descuento { get; init; }

    /// <summary>Valor de venta de la línea (<c>LineExtensionAmount</c>).</summary>
    public decimal ValorVenta { get; init; }

    public string? Afectacion { get; init; }

    public bool EsGratuita { get; init; }

    public decimal BolsasIcbper { get; init; }
}

/// <summary>Totales del comprobante: subtotales por tributo del documento y montos de <c>LegalMonetaryTotal</c>.</summary>
internal sealed record TotalesImpresos
{
    public decimal Gravado { get; init; }

    public decimal Exonerado { get; init; }

    public decimal Inafecto { get; init; }

    public decimal Exportacion { get; init; }

    public decimal Gratuito { get; init; }

    public decimal Igv { get; init; }

    public decimal Isc { get; init; }

    public decimal Icbper { get; init; }

    public decimal OtrosTributos { get; init; }

    public decimal Descuentos { get; init; }

    public decimal Cargos { get; init; }

    public decimal Anticipos { get; init; }

    public decimal Redondeo { get; init; }

    public decimal ImporteTotal { get; init; }
}

/// <summary>Guía de remisión remitente (09) leída del XML firmado (TRD §22).</summary>
internal sealed record GuiaImpresa
{
    public required ReferenciaCpe Referencia { get; init; }

    public required string SerieNumero { get; init; }

    public required DateOnly FechaEmision { get; init; }

    public TimeOnly? HoraEmision { get; init; }

    public required ParteImpresa Remitente { get; init; }

    public required ParteImpresa Destinatario { get; init; }

    public ParteImpresa? Comprador { get; init; }

    public ParteImpresa? Proveedor { get; init; }

    public string? CodigoMotivo { get; init; }

    public string? Motivo { get; init; }

    public DateOnly? FechaTraslado { get; init; }

    public string? CodigoModalidad { get; init; }

    public string? Modalidad { get; init; }

    public decimal? PesoBruto { get; init; }

    public string? UnidadPeso { get; init; }

    public string? Bultos { get; init; }

    public string? Partida { get; init; }

    public string? Llegada { get; init; }

    public IReadOnlyList<string> Vehiculos { get; init; } = [];

    public IReadOnlyList<ConductorImpreso> Conductores { get; init; } = [];

    public ParteImpresa? Transportista { get; init; }

    public IReadOnlyList<DocumentoRelacionadoImpreso> DocumentosRelacionados { get; init; } = [];

    public IReadOnlyList<string> Indicadores { get; init; } = [];

    public IReadOnlyList<BienImpreso> Bienes { get; init; } = [];

    public string? Observaciones { get; init; }

    public string? ValorResumen { get; init; }

    public required string EnlaceQr { get; init; }

    public EstadoSunatImpreso? Estado { get; init; }
}

internal sealed record ConductorImpreso(string TipoDocumento, string NumeroDocumento, string Nombre, string? Licencia, bool Principal);

internal sealed record BienImpreso(int Numero, string? Codigo, string Descripcion, string Unidad, decimal Cantidad);
