namespace PasheCPE;

/// <summary>
/// Guías de remisión electrónicas (TRD §6 y §17): generación, validación, envío por la API REST de SUNAT y consulta del
/// ticket de la guía remitente (09). La GRE solo existe en producción: con <c>Ambiente = Beta</c> no se envía salvo
/// que <see cref="OpcionesGre.PermitirEnvioConAmbienteBeta"/>.
/// </summary>
public interface IServicioGre
{
    /// <summary>
    /// Genera la guía: valida el modelo, escribe el XML (DATA), lo valida con el XSD y la XSL GRE 2.0 de SUNAT, lo firma
    /// (FIRMA), arma el ZIP de envío (ENVIO) y registra el historial (LOG). El QR se genera al aceptarse, con el enlace
    /// del CDR. No lanza por errores de validación ni de certificado. Una guía que ya se envió no se regenera.
    /// </summary>
    Task<ResultadoGeneracion> GenerarAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default);

    /// <summary>Solo la validación local (nivel 1) de la guía, sin escribir archivos.</summary>
    Task<ResultadoValidacion> ValidarAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default);

    /// <summary>
    /// Envía la guía ya generada (ZIP de ENVIO) y devuelve el ticket (<see cref="EstadoCpe.EnProceso"/>). Pide o reutiliza
    /// el token de la API (dura 1 hora). Si el historial ya tiene un resultado definitivo lo devuelve sin llamar a SUNAT;
    /// si ya tiene ticket, lo consulta en vez de reenviar. Nunca lanza por respuestas de SUNAT ni por fallas de red.
    /// </summary>
    Task<ResultadoEnvio> EnviarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct = default);

    /// <summary>Consulta una vez (con reintentos ante fallas) el ticket indicado o, si es null, el último del historial.</summary>
    Task<ResultadoEnvio> ConsultarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default);

    /// <summary>Consulta el ticket con espera progresiva hasta que deje de estar en proceso.</summary>
    Task<ResultadoEnvio> EsperarTicketAsync(EmisorCpe emisor, ReferenciaCpe referencia, string? ticket = null, CancellationToken ct = default);

    /// <summary>Genera, envía y espera el ticket; al aceptarse, el resultado trae el QR (enlace del CDR).</summary>
    Task<ResultadoEmision> EmitirAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct = default);
}
