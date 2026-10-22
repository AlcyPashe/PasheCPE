namespace PasheCPE;

/// <summary>Consulta del historial de envíos guardado en <c>LOG\yyyy\MM</c>, sin base de datos (PRD RF-19).</summary>
public interface IHistorialCpe
{
    /// <summary>Historial del documento, o null si no tiene.</summary>
    Task<HistorialCpe?> ObtenerAsync(ReferenciaCpe referencia, CancellationToken ct = default);

    /// <summary>
    /// Documentos del periodo que aún requieren acción: generados sin enviar, sin conexión, servicio no disponible,
    /// respuesta incierta o en proceso.
    /// </summary>
    IAsyncEnumerable<HistorialCpe> ListarPendientesAsync(int anio, int mes, CancellationToken ct = default);
}
