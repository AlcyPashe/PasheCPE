namespace PasheCPE;

/// <summary>
/// Comprobantes de pago electrónicos (TRD §6): generación, validación, envío por sendBill y consulta del CDR de facturas,
/// boletas y notas de crédito y débito; resumen diario y comunicación de baja (sendSummary y ticket).
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

    /// <summary>
    /// Genera la nota de crédito o débito con el mismo pipeline que la factura. Además valida el motivo, el único
    /// comprobante que modifica y su serie (R.S. 000048-2026) y, si se indica la fecha de emisión del documento modificado
    /// y este lo generó PasheCPE, su estado, moneda e importe total según el historial. Una nota de serie B con
    /// <see cref="ModoEnvioBoletas.ResumenDiario"/> se informa después en el resumen diario.
    /// </summary>
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);

    /// <summary>
    /// Genera el resumen diario de boletas y sus notas (RC) con los totales indicados: valida el modelo y el historial de
    /// cada documento, asigna los correlativos del día, divide en lotes de 500 líneas y, por cada lote, escribe el XML
    /// (DATA), lo valida (XSD, XSL de SUNAT y reglas complementarias), lo firma (FIRMA), arma el ZIP (ENVIO) y registra
    /// el historial (LOG). Devuelve un resultado por lote; no lanza por errores de validación ni de certificado.
    /// </summary>
    Task<IReadOnlyList<ResultadoGeneracion>> GenerarResumenAsync(EmisorCpe emisor, ResumenDiarioCpe resumen, CancellationToken ct = default);

    /// <summary>
    /// Igual, pero arma cada línea leyendo el XML firmado (FIRMA) de las boletas o notas ya generadas por PasheCPE, que
    /// deben ser de la fecha indicada: lo informado coincide con lo firmado.
    /// </summary>
    Task<IReadOnlyList<ResultadoGeneracion>> GenerarResumenAsync(EmisorCpe emisor, DateOnly fechaReferencia, IReadOnlyList<DocumentoResumenCpe> documentos, CancellationToken ct = default);

    /// <summary>
    /// Genera la comunicación de baja (RA) de facturas y notas de serie F emitidas en <see cref="ComunicacionBajaCpe.FechaReferencia"/>:
    /// valida el modelo, el plazo de 7 días desde la emisión (2957) y, si PasheCPE generó el documento, su historial
    /// (aceptado por SUNAT, no dado de baja ni en otra RA); asigna el correlativo del día, escribe el XML (DATA), lo
    /// valida (XSD y XSL de SUNAT), lo firma (FIRMA), arma el ZIP (ENVIO) y registra el historial (LOG), que anota la RA
    /// en el historial de cada documento. Las boletas y sus notas se anulan en el resumen diario con el estado 3. No
    /// lanza por errores de validación ni de certificado. Se envía con <see cref="EnviarAsync"/> y se consulta su ticket
    /// como el del resumen; al aceptarse, los documentos quedan <see cref="EstadoCpe.DeBaja"/>.
    /// </summary>
    Task<ResultadoGeneracion> GenerarBajaAsync(EmisorCpe emisor, ComunicacionBajaCpe baja, CancellationToken ct = default);

    /// <summary>Solo la validación local (nivel 1) de la factura o boleta, sin escribir archivos.</summary>
    Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);

    /// <summary>Solo la validación local (nivel 1) de la nota, incluida la del documento modificado, sin escribir archivos.</summary>
    Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);

    /// <summary>
    /// Envía el documento ya generado (ZIP de ENVIO) con sendBill o, si es un resumen diario (RC) o una comunicación de
    /// baja (RA), con sendSummary; nunca lo regenera. Si el historial tiene un resultado definitivo lo devuelve sin llamar
    /// a SUNAT; si el último envío quedó incierto, primero consulta su estado (getStatusCdr, o el ticket con getStatus).
    /// Nunca lanza por respuestas de SUNAT ni por fallas de red: el estado viene en <see cref="ResultadoEnvio.Estado"/>;
    /// el RC y la RA quedan <see cref="EstadoCpe.EnProceso"/> con su <see cref="ResultadoEnvio.Ticket"/>. Una baja fuera
    /// de plazo no se envía (PCPE-1510). Las boletas solo se envían con sendBill con <see cref="ModoEnvioBoletas.Individual"/>.
    /// </summary>
    Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);

    /// <summary>Genera y envía. Si la generación falla, <c>Envio.Estado = ErrorValidacionLocal</c> y no se envía.</summary>
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct = default);

    /// <summary>
    /// Genera y envía la nota por sendBill. Una nota de serie B con <see cref="ModoEnvioBoletas.ResumenDiario"/> queda
    /// generada y el envío devuelve el mismo resultado que una boleta en ese modo (se informa en el resumen diario).
    /// </summary>
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct = default);

    /// <summary>
    /// Consulta una vez (con hasta 3 reintentos ante fallas transitorias) el ticket del resumen (RC) o de la baja (RA)
    /// con getStatus: 98 en proceso, 0 con el CDR (aceptado u observado), 99 rechazado. Guarda el CDR en RPTA y registra
    /// el resultado en el historial del RC o la RA y en el de cada documento informado. Sin <paramref name="ticket"/> usa
    /// el último ticket del historial, así se puede consultar desde otro proceso u otro día.
    /// </summary>
    Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default);

    /// <summary>
    /// Espera y consulta el ticket con la espera progresiva de <see cref="OpcionesTickets.EsperasSegundos"/> (por defecto
    /// 2, 4, 8, 15 y 30 s) mientras siga en proceso. Si se agotan las esperas devuelve <see cref="EstadoCpe.EnProceso"/>
    /// con el ticket, que queda en el historial para consultarlo después.
    /// </summary>
    Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default);

    /// <summary>
    /// Consulta el estado y el CDR en SUNAT (getStatusCdr) y guarda el CDR en RPTA. Solo producción, y solo facturas y
    /// notas de serie F; en otro caso devuelve <see cref="EstadoCpe.ErrorServicio"/> con PCPE-4005.
    /// </summary>
    Task<ResultadoEnvio> ConsultarCdrAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);
}
