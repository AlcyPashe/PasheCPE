using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// Proveedor de logs que guarda todos los eventos de todas las categorías y niveles (Trace incluido), con el texto de la
/// excepción si la hay. Para auditar que ninguna credencial llegue a los logs.
/// </summary>
internal sealed class RegistroCapturado : ILoggerProvider
{
    private readonly ConcurrentQueue<(string Categoria, LogLevel Nivel, string Texto)> _eventos = new();

    public IReadOnlyList<(string Categoria, LogLevel Nivel, string Texto)> Eventos => [.. _eventos];

    /// <summary>Reemplaza el <c>NullLogger</c> de <see cref="ServiciosPrueba"/> por un logging real con este proveedor.</summary>
    public void Registrar(IServiceCollection servicios)
    {
        servicios.RemoveAll(typeof(ILogger<>));
        servicios.AddLogging(b => b.ClearProviders().AddProvider(this).SetMinimumLevel(LogLevel.Trace));
    }

    public ILogger CreateLogger(string categoryName) => new Registro(categoryName, _eventos);

    public void Dispose()
    {
    }

    private sealed class Registro(string categoria, ConcurrentQueue<(string, LogLevel, string)> eventos) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            eventos.Enqueue((categoria, logLevel, $"{formatter(state, exception)} {exception}"));
    }
}
