namespace PasheCPE;

/// <summary>Resultado de un envío o consulta. Nunca se lanza una excepción por respuestas de negocio o de red.</summary>
public sealed record ResultadoEnvio
{
    /// <summary>Estado tipado del documento.</summary>
    public EstadoCpe Estado { get; init; }

    /// <summary>Nivel de error derivado del estado (0 a 3); null si el documento aún no se envió.</summary>
    public int? Nivel => Estado.Nivel();

    /// <summary>Código de SUNAT u OSE: «0», «2324», «0111», «98»…</summary>
    public string? Codigo { get; init; }

    /// <summary>Texto literal devuelto por SUNAT u OSE.</summary>
    public string? Descripcion { get; init; }

    /// <summary>Descripción oficial del código según el catálogo de errores de SUNAT.</summary>
    public string? DescripcionCatalogo { get; init; }

    /// <summary>Mensaje en español claro para el cajero.</summary>
    public string MensajeUsuario { get; init; } = string.Empty;

    /// <summary>Observaciones del CDR (notas 4000 o más).</summary>
    public IReadOnlyList<ObservacionCpe> Observaciones { get; init; } = [];

    /// <summary>Errores de la validación local o del ticket (99).</summary>
    public IReadOnlyList<ErrorCpe> Errores { get; init; } = [];

    /// <summary>Ticket de resumen, baja o guía.</summary>
    public string? Ticket { get; init; }

    /// <summary>Valor resumen (DigestValue de la firma).</summary>
    public string? ValorResumen { get; init; }

    /// <summary>Contenido del código QR.</summary>
    public string? ContenidoQr { get; init; }

    /// <summary>Rutas de los archivos del documento en el almacén.</summary>
    public RutasCpe Rutas { get; init; } = RutasCpe.Vacio;

    /// <summary>Se puede volver a enviar más tarde.</summary>
    public bool EsReintentable { get; init; }

    /// <summary>Antes de reenviar hay que consultar el estado (getStatusCdr o ticket).</summary>
    public bool RequiereConsultaEstado { get; init; }

    /// <summary>Último día para enviar el documento a SUNAT.</summary>
    public DateOnly? FechaLimiteEnvio { get; init; }

    /// <summary>Detalle técnico: endpoint, operación, HTTP, fault, duración.</summary>
    public DetalleTecnico? Tecnico { get; init; }

    /// <summary>Fecha y hora del proceso en Lima.</summary>
    public DateTimeOffset FechaProceso { get; init; }

    /// <summary>Intentos registrados en el historial del documento.</summary>
    public IReadOnlyList<IntentoEnvio> Historial { get; init; } = [];
}

/// <summary>Detalle técnico de una operación de red.</summary>
public sealed record DetalleTecnico
{
    /// <summary>URL invocada.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Operación: sendBill, sendSummary, getStatus, getStatusCdr, token, envío GRE…</summary>
    public string? Operacion { get; init; }

    /// <summary>Código HTTP de la respuesta.</summary>
    public int? Http { get; init; }

    /// <summary>faultcode del SOAP.</summary>
    public string? FaultCode { get; init; }

    /// <summary>faultstring del SOAP.</summary>
    public string? FaultString { get; init; }

    /// <summary>Duración de la operación en milisegundos.</summary>
    public long? DuracionMs { get; init; }

    /// <summary>Tipo y mensaje de la excepción técnica, si la hubo.</summary>
    public string? Excepcion { get; init; }
}
