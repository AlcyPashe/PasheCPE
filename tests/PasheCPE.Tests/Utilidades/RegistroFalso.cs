using Microsoft.Extensions.Logging;

namespace PasheCPE.Tests.Utilidades;

/// <summary>ILogger que guarda los eventos para inspeccionarlos en la prueba.</summary>
internal sealed class RegistroFalso<T> : ILogger<T>
{
    private readonly List<(LogLevel Nivel, EventId Evento, string Mensaje)> _eventos = [];

    public IReadOnlyList<(LogLevel Nivel, EventId Evento, string Mensaje)> Eventos
    {
        get
        {
            lock (_eventos)
            {
                return [.. _eventos];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_eventos)
        {
            _eventos.Add((logLevel, eventId, formatter(state, exception)));
        }
    }
}
