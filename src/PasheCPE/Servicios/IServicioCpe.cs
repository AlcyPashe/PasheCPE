namespace PasheCPE;

/// <summary>
/// Comprobantes de pago electrónicos (TRD §6). En esta versión: generación y validación de facturas y boletas; el
/// envío, las notas, el resumen diario y la baja se agregan en las versiones siguientes.
/// </summary>
public interface IServicioCpe
{
    /// <summary>
    /// Genera la factura o boleta: valida el modelo, calcula, escribe el XML (DATA), lo valida con el XSD, las XSL de
    /// SUNAT y las reglas complementarias, lo firma (FIRMA), arma el ZIP de envío (ENVIO) y el QR (QR), y registra el
    /// historial (LOG). No lanza por errores de validación ni de certificado: vienen en
    /// <see cref="ResultadoGeneracion.Errores"/>. Un documento que ya se envió no se regenera.
    /// </summary>
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);

    /// <summary>Solo la validación local (nivel 1) de la factura o boleta, sin escribir archivos.</summary>
    Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);
}
