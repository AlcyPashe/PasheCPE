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

    [LoggerMessage(EventId = 1501, Level = LogLevel.Debug,
        Message = "SOAP {Operacion} → {Endpoint}: {Sobre}")]
    public static partial void SobreSoapEnviado(ILogger registro, string operacion, string endpoint, SobreEnmascarado sobre);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Debug,
        Message = "SOAP {Operacion} ← HTTP {Http}: {Sobre}")]
    public static partial void SobreSoapRecibido(ILogger registro, string operacion, int http, SobreEnmascarado sobre);

    [LoggerMessage(EventId = 1503, Level = LogLevel.Warning,
        Message = "Falla de red en {Operacion} ({Endpoint}): {Falla}. {Detalle}")]
    public static partial void FallaRedSoap(ILogger registro, string operacion, string endpoint, Transporte.Soap.FallaRed falla, string detalle);

    [LoggerMessage(EventId = 1504, Level = LogLevel.Information,
        Message = "{Operacion} de {Nombre}: {Estado} {Codigo} {Descripcion}")]
    public static partial void EnvioResultado(ILogger registro, string operacion, string nombre, EstadoCpe estado, string? codigo, string? descripcion);

    [LoggerMessage(EventId = 1505, Level = LogLevel.Error,
        Message = "{Operacion} de {Nombre}: {Estado} {Codigo} {Descripcion}")]
    public static partial void EnvioFallido(ILogger registro, string operacion, string nombre, EstadoCpe estado, string? codigo, string? descripcion);

    [LoggerMessage(EventId = 1506, Level = LogLevel.Warning,
        Message = "Reintento {Intento} de {Operacion} de {Nombre} tras {Estado} {Codigo}.")]
    public static partial void Reintento(ILogger registro, int intento, string operacion, string nombre, EstadoCpe estado, string? codigo);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Debug,
        Message = "GRE {Operacion} → {Url}: {Cuerpo}")]
    public static partial void SolicitudGre(ILogger registro, string operacion, string url, SobreEnmascarado cuerpo);

    [LoggerMessage(EventId = 1602, Level = LogLevel.Debug,
        Message = "GRE {Operacion} ← HTTP {Http}: {Cuerpo}")]
    public static partial void RespuestaGre(ILogger registro, string operacion, int http, SobreEnmascarado cuerpo);

    [LoggerMessage(EventId = 1603, Level = LogLevel.Information,
        Message = "Token GRE obtenido para {Usuario}; se renueva desde {Vence:u}.")]
    public static partial void TokenGreObtenido(ILogger registro, string usuario, DateTimeOffset vence);
}
