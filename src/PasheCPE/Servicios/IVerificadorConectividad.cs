namespace PasheCPE;

/// <summary>
/// Verificación de conectividad antes de enviar (PRD RF-14, TRD §13): distingue «sin Internet» de «el servicio de
/// SUNAT u OSE no responde». No usa ping (ICMP suele estar bloqueado).
/// </summary>
public interface IVerificadorConectividad
{
    /// <summary>Disponibilidad del servicio de comprobantes del canal en el ambiente configurado.</summary>
    Task<EstadoConectividad> VerificarAsync(CanalEnvio canal, CancellationToken ct = default);
}
