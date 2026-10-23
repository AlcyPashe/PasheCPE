namespace PasheCPE;

/// <summary>
/// Resumen diario de boletas y sus notas (RC). La librería asigna los correlativos y divide en lotes de 500 líneas.
/// </summary>
public sealed record ResumenDiarioCpe
{
    /// <summary>Fecha de emisión de los documentos informados.</summary>
    public required DateOnly FechaReferencia { get; init; }

    /// <summary>Fecha de generación del resumen; si es null se usa la fecha actual de Lima.</summary>
    public DateOnly? FechaGeneracion { get; init; }

    /// <summary>Documentos informados.</summary>
    public required IReadOnlyList<LineaResumenCpe> Lineas { get; init; }
}

/// <summary>Documento informado en el resumen diario, con sus totales (incluidos los campos vigentes desde 2027).</summary>
public sealed record LineaResumenCpe
{
    /// <summary>Tipo de documento: 03, 07 u 08.</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Serie del documento.</summary>
    public required string Serie { get; init; }

    /// <summary>Número del documento.</summary>
    public required long Numero { get; init; }

    /// <summary>Estado del ítem (catálogo N.° 19): <see cref="EstadoItemResumen"/>.</summary>
    public required string Estado { get; init; }

    /// <summary>Adquiriente (desde 2027 se exige también la razón social).</summary>
    public required ClienteCpe Cliente { get; init; }

    /// <summary>Moneda ISO 4217.</summary>
    public string Moneda { get; init; } = "PEN";

    /// <summary>Boleta que modifica la nota (solo para 07 y 08).</summary>
    public DocumentoModificadoCpe? DocumentoModificado { get; init; }

    /// <summary>Total de operaciones gravadas.</summary>
    public decimal TotalGravado { get; init; }

    /// <summary>Total de operaciones exoneradas.</summary>
    public decimal TotalExonerado { get; init; }

    /// <summary>Total de operaciones inafectas.</summary>
    public decimal TotalInafecto { get; init; }

    /// <summary>Total de exportaciones.</summary>
    public decimal TotalExportacion { get; init; }

    /// <summary>Operaciones gratuitas gravadas (desagregadas desde 2027).</summary>
    public decimal TotalGratuitoGravado { get; init; }

    /// <summary>Operaciones gratuitas exoneradas (desagregadas desde 2027).</summary>
    public decimal TotalGratuitoExonerado { get; init; }

    /// <summary>Operaciones gratuitas inafectas (desagregadas desde 2027).</summary>
    public decimal TotalGratuitoInafecto { get; init; }

    /// <summary>Exportaciones gratuitas (desagregadas desde 2027).</summary>
    public decimal TotalGratuitoExportacion { get; init; }

    /// <summary>Otros cargos.</summary>
    public decimal OtrosCargos { get; init; }

    /// <summary>Total del IGV.</summary>
    public decimal Igv { get; init; }

    /// <summary>IGV de las operaciones gratuitas (desde 2027).</summary>
    public decimal IgvGratuitas { get; init; }

    /// <summary>Tasa del IGV aplicada (desde 2027); si es null la determina la librería.</summary>
    public decimal? TasaIgv { get; init; }

    /// <summary>Total del ISC.</summary>
    public decimal Isc { get; init; }

    /// <summary>Total del ICBPER.</summary>
    public decimal Icbper { get; init; }

    /// <summary>Importe total.</summary>
    public required decimal ImporteTotal { get; init; }
}

/// <summary>
/// Boleta o nota ya generada por PasheCPE que se informa en el resumen diario: la librería arma la línea leyendo su XML
/// firmado (FIRMA), así lo informado coincide con lo firmado.
/// </summary>
public sealed record DocumentoResumenCpe
{
    /// <summary>Tipo de documento: 03, 07 u 08.</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Serie del documento.</summary>
    public required string Serie { get; init; }

    /// <summary>Número del documento.</summary>
    public required long Numero { get; init; }

    /// <summary>Estado del ítem (catálogo N.° 19): <see cref="EstadoItemResumen"/>; por defecto, adicionar.</summary>
    public string Estado { get; init; } = EstadoItemResumen.Adicionar;
}

/// <summary>Comunicación de baja (RA) de comprobantes aceptados.</summary>
public sealed record ComunicacionBajaCpe
{
    /// <summary>Fecha de emisión de los documentos dados de baja.</summary>
    public required DateOnly FechaReferencia { get; init; }

    /// <summary>Fecha de generación de la comunicación; si es null se usa la fecha actual de Lima.</summary>
    public DateOnly? FechaGeneracion { get; init; }

    /// <summary>Documentos dados de baja.</summary>
    public required IReadOnlyList<DocumentoBajaCpe> Documentos { get; init; }
}

/// <summary>Documento dado de baja.</summary>
public sealed record DocumentoBajaCpe
{
    /// <summary>Tipo de documento (catálogo N.° 01).</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Serie.</summary>
    public required string Serie { get; init; }

    /// <summary>Número.</summary>
    public required long Numero { get; init; }

    /// <summary>Motivo de la baja.</summary>
    public required string Motivo { get; init; }
}
