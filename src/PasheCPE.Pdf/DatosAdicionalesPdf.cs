namespace PasheCPE.Pdf;

/// <summary>
/// Datos que el sistema de ventas imprime y que no van en el XML (vendedor, caja, pedido…). Se muestran en el bloque
/// «Información adicional».
/// </summary>
public sealed record DatosAdicionalesPdf
{
    /// <summary>Pares etiqueta/valor, en el orden indicado.</summary>
    public IReadOnlyList<CampoAdicionalPdf> Campos { get; init; } = [];

    /// <summary>Observaciones en texto libre.</summary>
    public string? Observaciones { get; init; }
}

/// <summary>Un dato adicional del PDF: «Vendedor: JUAN PÉREZ».</summary>
public sealed record CampoAdicionalPdf(string Etiqueta, string Valor);
