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

    /// <summary>
    /// Último día para enviar el documento ya generado, contado desde su fecha. En las notas el tipo modificado se deduce
    /// de la serie, que la validación del modelo hace coherente: F modifica una factura y B, una boleta.
    /// </summary>
    public DateOnly? FechaLimite(ReferenciaCpe referencia)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        return FechaLimite(referencia.TipoDocumento, TipoModificado(referencia.TipoDocumento, referencia.Serie), referencia.Fecha);
    }

    /// <summary>Tipo que modifica una nota según su serie (F → 01, B → 03); null si no es una nota.</summary>
    public static string? TipoModificado(string tipoDocumento, string? serie) =>
        tipoDocumento is TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito
            ? serie is ['B', ..] ? TipoDocumentoSunat.BoletaDeVenta : TipoDocumentoSunat.Factura
            : null;
}
