using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// Proveedor de servicios de PasheCPE para pruebas: almacén temporal y reloj fijo (por defecto el 22/10/2026 a las
/// 10:00 de Lima), para que los plazos de envío no dependan del día en que corren las pruebas.
/// </summary>
internal sealed class ServiciosPrueba : IDisposable
{
    private readonly ServiceProvider _proveedor;

    public ServiciosPrueba(Action<OpcionesPasheCpe>? configurar = null, DateTimeOffset? ahora = null, Action<IServiceCollection>? registrar = null)
    {
        Almacen = new AlmacenTemporal();
        Reloj = new FakeTimeProvider(ahora ?? new DateTimeOffset(2026, 10, 22, 15, 0, 0, TimeSpan.Zero));
        var servicios = new ServiceCollection();
        servicios.AddSingleton<TimeProvider>(Reloj);
        servicios.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        registrar?.Invoke(servicios);
        servicios.AddPasheCpe(o =>
        {
            o.RutaBase = Almacen.Ruta;
            configurar?.Invoke(o);
        });
        _proveedor = servicios.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public AlmacenTemporal Almacen { get; }

    public FakeTimeProvider Reloj { get; }

    public T Obtener<T>()
        where T : notnull => _proveedor.GetRequiredService<T>();

    public void Dispose()
    {
        _proveedor.Dispose();
        Almacen.Dispose();
    }
}
