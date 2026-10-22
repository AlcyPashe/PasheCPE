using Microsoft.Extensions.Logging;

namespace PasheCPE.Diagnostico;

/// <summary>Eventos de log de PasheCPE (LoggerMessage generado). Nunca incluyen credenciales.</summary>
internal static partial class RegistroEventos
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning,
        Message = "El endpoint {Url} ({Canal}, {Ambiente}, {Servicio}) aún no está verificado con un envío real; confírmelo o sobrescríbalo en PasheCPE:Endpoints.")]
    public static partial void EndpointNoVerificado(ILogger registro, string url, string canal, Ambiente ambiente, ServicioSunat servicio);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning,
        Message = "Se omitió el historial {Ruta} porque no se pudo leer: {Motivo}")]
    public static partial void HistorialIlegible(ILogger registro, string ruta, string motivo);
}
