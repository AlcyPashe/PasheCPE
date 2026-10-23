using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PasheCPE.Diagnostico;
using PasheCPE.Empaquetado;

namespace PasheCPE.Almacenamiento;

/// <summary>
/// Historial JSON por documento en <c>LOG\yyyy\MM\{nombre}.json</c> (PRD RF-19). El acceso se serializa por
/// documento dentro del proceso con un número fijo de semáforos (asignados por hash del nombre), así la memoria no
/// crece en un servicio de larga duración; la escritura es atómica.
/// </summary>
internal sealed class RegistroHistorial(AlmacenCpe almacen, ILogger<RegistroHistorial> registro) : IDisposable
{
    private const int _cantidadBloqueos = 64;

    private readonly SemaphoreSlim[] _bloqueos = [.. Enumerable.Range(0, _cantidadBloqueos).Select(_ => new SemaphoreSlim(1, 1))];

    /// <summary>Agrega un intento (si lo hay) y actualiza el estado actual y el ticket del documento.</summary>
    public async Task<HistorialCpe> RegistrarAsync(ReferenciaCpe referencia, EstadoCpe estadoActual, IntentoEnvio? intento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        var ruta = Ruta(referencia);
        var bloqueo = _bloqueos[(StringComparer.OrdinalIgnoreCase.GetHashCode(ruta) & int.MaxValue) % _cantidadBloqueos];
        await bloqueo.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var actual = await LeerAsync(ruta, ct).ConfigureAwait(false) ?? new HistorialCpe { Referencia = referencia };
            var nuevo = actual with
            {
                Referencia = referencia,
                EstadoActual = estadoActual,
                Ticket = intento?.Ticket ?? actual.Ticket,
                Intentos = intento is null ? actual.Intentos : [.. actual.Intentos, intento],
            };
            await AlmacenCpe.EscribirAsync(ruta, JsonSerializer.SerializeToUtf8Bytes(nuevo, HistorialJson.Default.HistorialCpe), ct).ConfigureAwait(false);
            return nuevo;
        }
        finally
        {
            bloqueo.Release();
        }
    }

    public Task<HistorialCpe?> ObtenerAsync(ReferenciaCpe referencia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        return LeerAsync(Ruta(referencia), ct);
    }

    /// <summary>Historiales del periodo; los archivos ilegibles se omiten con una advertencia en el log.</summary>
    public async IAsyncEnumerable<HistorialCpe> ListarAsync(int anio, int mes, [EnumeratorCancellation] CancellationToken ct)
    {
        var carpeta = almacen.CarpetaPeriodo(CarpetaCpe.Log, anio, mes);
        if (!Directory.Exists(carpeta))
        {
            yield break;
        }

        foreach (var ruta in Directory.EnumerateFiles(carpeta, "*.json").Order(StringComparer.OrdinalIgnoreCase))
        {
            HistorialCpe? historial = null;
            try
            {
                historial = await LeerAsync(ruta, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is JsonException or IOException or InvalidDataException)
            {
                RegistroEventos.HistorialIlegible(registro, ruta, ex.Message);
            }

            if (historial is not null)
            {
                yield return historial;
            }
        }
    }

    public void Dispose()
    {
        foreach (var bloqueo in _bloqueos)
        {
            bloqueo.Dispose();
        }
    }

    /// <summary>Ruta del historial del documento: LOG\yyyy\MM\{nombre}.json.</summary>
    public string Ruta(ReferenciaCpe referencia) => almacen.Ruta(CarpetaCpe.Log, referencia, NombreArchivoCpe.Historial(referencia));

    private static async Task<HistorialCpe?> LeerAsync(string ruta, CancellationToken ct)
    {
        var contenido = await AlmacenCpe.LeerAsync(ruta, ct).ConfigureAwait(false);
        return contenido is null ? null : JsonSerializer.Deserialize(contenido, HistorialJson.Default.HistorialCpe);
    }
}

/// <summary>Consulta pública del historial (<see cref="IHistorialCpe"/>).</summary>
internal sealed class ServicioHistorial(RegistroHistorial registro) : IHistorialCpe
{
    public Task<HistorialCpe?> ObtenerAsync(ReferenciaCpe referencia, CancellationToken ct = default) =>
        registro.ObtenerAsync(referencia, ct);

    public async IAsyncEnumerable<HistorialCpe> ListarPendientesAsync(int anio, int mes, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var historial in registro.ListarAsync(anio, mes, ct).ConfigureAwait(false))
        {
            if (historial.EstadoActual.EsPendiente())
            {
                yield return historial;
            }
        }
    }
}
