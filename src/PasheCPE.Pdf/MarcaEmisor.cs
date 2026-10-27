namespace PasheCPE.Pdf;

/// <summary>
/// Datos de marca del emisor para la representación impresa (TRD §22): logo, color principal y datos de contacto. No
/// forman parte del XML firmado.
/// </summary>
public sealed record MarcaEmisor
{
    /// <summary>Sin logo ni datos de contacto, con el color por defecto.</summary>
    public static MarcaEmisor SinMarca { get; } = new();

    /// <summary>Logo (PNG o JPEG). Tiene prioridad sobre <see cref="RutaLogo"/>.</summary>
    public ReadOnlyMemory<byte>? Logo { get; init; }

    /// <summary>Ruta del logo (PNG o JPEG), por ejemplo desde la configuración; vacía, sin logo.</summary>
    public string? RutaLogo { get; init; }

    /// <summary>Color principal en hexadecimal (#RRGGBB): recuadro del número, títulos y encabezados de tabla.</summary>
    public string ColorPrincipal { get; init; } = "#1F3A5F";

    /// <summary>Teléfonos.</summary>
    public IReadOnlyList<string> Telefonos { get; init; } = [];

    /// <summary>Correo electrónico.</summary>
    public string? Correo { get; init; }

    /// <summary>Sitio web.</summary>
    public string? Web { get; init; }

    /// <summary>Texto libre al pie (condiciones, cuentas bancarias…).</summary>
    public string? TextoPie { get; init; }
}
