namespace PasheCPE;

/// <summary>Historial de un documento: <c>LOG\yyyy\MM\{nombre}.json</c> (PRD RF-19). Nunca contiene credenciales.</summary>
public sealed record HistorialCpe
{
    /// <summary>Documento al que pertenece.</summary>
    public required ReferenciaCpe Referencia { get; init; }

    /// <summary>Estado después del último intento.</summary>
    public EstadoCpe EstadoActual { get; init; }

    /// <summary>Último ticket recibido (resumen, baja o guía).</summary>
    public string? Ticket { get; init; }

    /// <summary>Intentos en orden cronológico.</summary>
    public IReadOnlyList<IntentoEnvio> Intentos { get; init; } = [];

    /// <summary>
    /// Documentos informados (resumen diario y comunicación de baja): su resultado se registra también en sus
    /// historiales.
    /// </summary>
    public IReadOnlyList<DocumentoInformadoCpe>? Documentos { get; init; }

    /// <summary>
    /// Comunicación de baja (RA) que informa este documento, desde que se genera hasta que concluye; si la RA se
    /// rechaza vuelve a null y el documento se puede informar en otra. Con la RA aceptada el estado es
    /// <see cref="EstadoCpe.DeBaja"/>.
    /// </summary>
    public ReferenciaCpe? Baja { get; init; }
}

/// <summary>Documento informado en un resumen diario o en una comunicación de baja.</summary>
public sealed record DocumentoInformadoCpe
{
    /// <summary>Documento informado (su fecha es la de emisión).</summary>
    public required ReferenciaCpe Referencia { get; init; }

    /// <summary>Estado del ítem (catálogo N.° 19): 1 adicionar, 2 modificar, 3 anulado; en la baja siempre 3.</summary>
    public required string Estado { get; init; }
}

/// <summary>Un intento de envío o consulta.</summary>
public sealed record IntentoEnvio
{
    /// <summary>Fecha y hora en Lima.</summary>
    public DateTimeOffset Fecha { get; init; }

    /// <summary>Operación: generar, sendBill, sendSummary, getStatus, getStatusCdr, envío GRE…</summary>
    public required string Operacion { get; init; }

    /// <summary>Canal: SUNAT u OSE:{nombre}.</summary>
    public string? Canal { get; init; }

    /// <summary>URL invocada.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Duración en milisegundos.</summary>
    public long DuracionMs { get; init; }

    /// <summary>Estado resultante.</summary>
    public EstadoCpe Estado { get; init; }

    /// <summary>Código devuelto.</summary>
    public string? Codigo { get; init; }

    /// <summary>Descripción devuelta.</summary>
    public string? Descripcion { get; init; }

    /// <summary>Código HTTP.</summary>
    public int? Http { get; init; }

    /// <summary>Ticket recibido.</summary>
    public string? Ticket { get; init; }
}

/// <summary>Resultado de la verificación de conectividad (PRD RF-14).</summary>
public sealed record EstadoConectividad
{
    /// <summary>Disponibilidad del destino.</summary>
    public required Disponibilidad Disponibilidad { get; init; }

    /// <summary>Host verificado.</summary>
    public required string Destino { get; init; }

    /// <summary>Detalle de la falla, si la hubo.</summary>
    public string? Detalle { get; init; }

    /// <summary>Fecha y hora de la verificación en Lima.</summary>
    public DateTimeOffset Fecha { get; init; }

    /// <summary>true si el resultado viene de la caché de unos segundos.</summary>
    public bool DesdeCache { get; init; }
}

/// <summary>Disponibilidad de un servicio destino.</summary>
public enum Disponibilidad
{
    /// <summary>Hay Internet y el servicio responde.</summary>
    Disponible,

    /// <summary>Sin red o sin Internet.</summary>
    SinConexionInternet,

    /// <summary>Hay Internet pero el servicio no responde.</summary>
    ServicioNoDisponible,
}
