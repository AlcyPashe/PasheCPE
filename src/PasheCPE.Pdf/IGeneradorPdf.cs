namespace PasheCPE.Pdf;

/// <summary>
/// Representación impresa A4 (PRD RF-12, TRD §22) de facturas, boletas, notas de crédito y débito y guías de remisión
/// remitente, armada desde el XML firmado de <c>FIRMA\</c> y el CDR de <c>RPTA\</c>; se puede regenerar en cualquier
/// momento. Se guarda en <c>REPO\yyyy\MM\{nombre}.pdf</c>.
/// </summary>
public interface IGeneradorPdf
{
    /// <summary>Genera (o vuelve a generar) el PDF del documento. Nunca lanza por errores del documento.</summary>
    Task<ResultadoPdf> GenerarAsync(ReferenciaCpe referencia, MarcaEmisor marca, CancellationToken ct = default);
}
