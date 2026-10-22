using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PasheCPE.Configuracion;

namespace PasheCPE;

/// <summary>
/// Uso de PasheCPE sin inyección de dependencias (consola, herramientas). Arma internamente el mismo
/// <see cref="IServiceCollection"/> que <c>AddPasheCpe</c>, de modo que con o sin DI se ejecuta el mismo código.
/// <c>Dispose</c> libera el proveedor de servicios y los HttpClient.
/// </summary>
/// <example>
/// <code>
/// using var pashe = PasheCpe.Crear(new OpcionesPasheCpe { RutaBase = @"D:\CPE", Ambiente = Ambiente.Beta });
/// var pendientes = pashe.Historial.ListarPendientesAsync(2026, 10);
/// </code>
/// </example>
public sealed class PasheCpe : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _proveedor;

    private PasheCpe(ServiceProvider proveedor)
    {
        _proveedor = proveedor;
        Historial = proveedor.GetRequiredService<IHistorialCpe>();
    }

    /// <summary>Historial de envíos.</summary>
    public IHistorialCpe Historial { get; }

    /// <summary>
    /// Crea la instancia. Las opciones se validan de inmediato (<see cref="OptionsValidationException"/> si no son
    /// válidas). Si no se indica <paramref name="fabricaRegistros"/>, no se registran logs.
    /// </summary>
    public static PasheCpe Crear(OpcionesPasheCpe opciones, ILoggerFactory? fabricaRegistros = null, TimeProvider? tiempo = null)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        var validacion = new ValidadorOpciones().Validate(Options.DefaultName, opciones);
        if (validacion.Failed)
        {
            throw new OptionsValidationException(Options.DefaultName, typeof(OpcionesPasheCpe), validacion.Failures);
        }

        var servicios = new ServiceCollection();
        servicios.AddSingleton(fabricaRegistros ?? NullLoggerFactory.Instance);
        servicios.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        if (tiempo is not null)
        {
            servicios.AddSingleton(tiempo);
        }

        servicios.AddSingleton(Options.Create(opciones));
        servicios.AgregarNucleoPasheCpe();
        return new PasheCpe(servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }));
    }

    /// <inheritdoc/>
    public void Dispose() => _proveedor.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _proveedor.DisposeAsync();
}
