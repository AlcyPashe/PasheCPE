namespace PasheCPE.Pdf;

/// <summary>Resultado de generar el PDF. Nunca se lanza una excepción: los errores son <c>PCPE-50xx</c>.</summary>
public sealed record ResultadoPdf
{
    /// <summary>Documento.</summary>
    public required ReferenciaCpe Referencia { get; init; }

    /// <summary>true si se generó el PDF.</summary>
    public bool Exito => Errores.Count == 0;

    /// <summary>Ruta del PDF en <c>REPO\yyyy\MM\{nombre}.pdf</c>.</summary>
    public string? Ruta { get; init; }

    /// <summary>Errores que impidieron generar el PDF.</summary>
    public IReadOnlyList<ErrorCpe> Errores { get; init; } = [];

    /// <summary>Advertencias (por ejemplo, un logo que no se pudo usar).</summary>
    public IReadOnlyList<ObservacionCpe> Advertencias { get; init; } = [];
}
