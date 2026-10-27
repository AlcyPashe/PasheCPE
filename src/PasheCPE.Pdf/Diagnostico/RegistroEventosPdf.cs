using Microsoft.Extensions.Logging;

namespace PasheCPE.Pdf.Diagnostico;

/// <summary>Eventos de log de PasheCPE.Pdf (LoggerMessage generado), en el rango 5000.</summary>
internal static partial class RegistroEventosPdf
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information,
        Message = "PDF {Nombre} generado en {Ruta}.")]
    public static partial void PdfGenerado(ILogger registro, string nombre, string ruta);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning,
        Message = "No se generó el PDF {Nombre}: {Codigo} {Descripcion}")]
    public static partial void PdfNoGenerado(ILogger registro, string nombre, string codigo, string descripcion);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error,
        Message = "Falla al dibujar el PDF {Nombre}.")]
    public static partial void PdfFallido(ILogger registro, string nombre, Exception excepcion);
}
