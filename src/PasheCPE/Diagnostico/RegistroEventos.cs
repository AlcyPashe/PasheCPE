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

    [LoggerMessage(EventId = 1201, Level = LogLevel.Error,
        Message = "No se pudo precalentar PasheCPE (XSL, XSD o serializadores); se compilarán en la primera validación.")]
    public static partial void PrecalentamientoFallido(ILogger registro, Exception excepcion);

    [LoggerMessage(EventId = 1301, Level = LogLevel.Warning,
        Message = "El certificado {Archivo} vence el {Vencimiento} (hora de Lima); renuévelo antes de esa fecha.")]
    public static partial void CertificadoPorVencer(ILogger registro, string archivo, string vencimiento);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Information,
        Message = "Documento {Nombre} generado y firmado (valor resumen {ValorResumen}).")]
    public static partial void DocumentoGenerado(ILogger registro, string nombre, string valorResumen);
}
