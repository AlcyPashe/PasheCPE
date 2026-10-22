using Microsoft.Extensions.Options;

namespace PasheCPE.Catalogos;

/// <summary>
/// Plazos máximos de envío (<c>plazos.json</c> o <see cref="OpcionesPasheCpe.Plazos"/>), en días calendario
/// contados desde el día siguiente: emitida el 22, con plazo de 3 días, vence el 25.
/// </summary>
internal sealed class TablaPlazos(IOptions<OpcionesPasheCpe> opciones)
{
    private readonly PlazoEnvio[] _plazos = opciones.Value.Plazos.Count > 0
        ? [.. opciones.Value.Plazos]
        : [.. RecursosEmbebidos.Leer("plazos.json", DatosJson.Default.ArchivoPlazos).Plazos];

    /// <summary>Plazo del documento; para notas se prefiere el que coincide con el tipo modificado.</summary>
    public PlazoEnvio? Obtener(string documento, string? documentoModificado = null) =>
        Array.Find(_plazos, p => p.Documento == documento && p.Modifica == documentoModificado)
        ?? Array.Find(_plazos, p => p.Documento == documento && p.Modifica is null);

    /// <summary>Último día para enviar, contado desde <paramref name="fechaBase"/> (emisión o recepción del CDR).</summary>
    public DateOnly? FechaLimite(string documento, string? documentoModificado, DateOnly fechaBase) =>
        Obtener(documento, documentoModificado) is { } plazo ? fechaBase.AddDays(plazo.Dias) : null;
}
