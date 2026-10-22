using Microsoft.Extensions.Options;

namespace PasheCPE.Tests.Utilidades;

/// <summary>Almacén CPE en una carpeta temporal que se borra al terminar la prueba.</summary>
internal sealed class AlmacenTemporal : IDisposable
{
    public AlmacenTemporal()
    {
        Ruta = Path.Combine(Path.GetTempPath(), "PasheCPE.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Ruta);
    }

    public string Ruta { get; }

    public OpcionesPasheCpe Opciones(Action<OpcionesPasheCpe>? configurar = null)
    {
        var opciones = new OpcionesPasheCpe { RutaBase = Ruta };
        configurar?.Invoke(opciones);
        return opciones;
    }

    public IOptions<OpcionesPasheCpe> OpcionesDi(Action<OpcionesPasheCpe>? configurar = null) =>
        Microsoft.Extensions.Options.Options.Create(Opciones(configurar));

    public void Dispose()
    {
        if (Directory.Exists(Ruta))
        {
            Directory.Delete(Ruta, recursive: true);
        }
    }
}
