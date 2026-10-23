namespace PasheCPE;

/// <summary>
/// Comprobantes de pago electrónicos (TRD §6). En esta versión: generación, validación, envío por sendBill y consulta
/// del CDR de facturas y boletas; las notas, el resumen diario y la baja se agregan en las versiones siguientes.
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

    /// <summary>
    /// Envía el documento ya generado (ZIP de ENVIO) con sendBill; nunca lo regenera. Si el historial tiene un resultado
    /// definitivo lo devuelve sin llamar a SUNAT; si el último envío quedó incierto, primero consulta su estado. Nunca
    /// lanza por respuestas de SUNAT ni por fallas de red: el estado viene en <see cref="ResultadoEnvio.Estado"/>.
    /// Las boletas solo se envían así con <see cref="ModoEnvioBoletas.Individual"/>.
    /// </summary>
    Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);

    /// <summary>Genera y envía. Si la generación falla, <c>Envio.Estado = ErrorValidacionLocal</c> y no se envía.</summary>
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);

    /// <summary>
    /// Consulta el estado y el CDR en SUNAT (getStatusCdr) y guarda el CDR en RPTA. Solo producción, y solo facturas y
    /// notas de serie F; en otro caso devuelve <see cref="EstadoCpe.ErrorServicio"/> con PCPE-4005.
    /// </summary>
    Task<ResultadoEnvio> ConsultarCdrAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);
}
