namespace PasheCPE;

/// <summary>Resultado de generar un documento (XML, validación, firma, ZIP y QR).</summary>
public sealed record ResultadoGeneracion
{
    /// <summary>Referencia del documento generado.</summary>
    public required ReferenciaCpe Referencia { get; init; }

    /// <summary>Nombre oficial del archivo, sin extensión.</summary>
    public string NombreArchivo => Referencia.NombreArchivo;

    /// <summary>true si no hubo errores.</summary>
    public bool Exito => Errores.Count == 0;

    /// <summary>Valor resumen (DigestValue de la firma).</summary>
    public string? ValorResumen { get; init; }

    /// <summary>Contenido del código QR.</summary>
    public string? ContenidoQr { get; init; }

    /// <summary>Rutas de los archivos generados.</summary>
    public RutasCpe Rutas { get; init; } = RutasCpe.Vacio;

    /// <summary>Errores que impiden el envío.</summary>
    public IReadOnlyList<ErrorCpe> Errores { get; init; } = [];

    /// <summary>Advertencias que no impiden el envío (certificado por vencer, tasa aplicada por defecto…).</summary>
    public IReadOnlyList<ObservacionCpe> Advertencias { get; init; } = [];

    /// <summary>Último día para enviar el documento a SUNAT.</summary>
    public DateOnly? FechaLimiteEnvio { get; init; }

    /// <summary>Fecha y hora del proceso en Lima.</summary>
    public DateTimeOffset FechaProceso { get; init; }
}

/// <summary>Resultado de generar y enviar un documento.</summary>
public sealed record ResultadoEmision
{
    /// <summary>Resultado de la generación.</summary>
    public required ResultadoGeneracion Generacion { get; init; }

    /// <summary>Resultado del envío (ErrorValidacionLocal si la generación falló).</summary>
    public required ResultadoEnvio Envio { get; init; }
}

/// <summary>Resultado de la validación local (nivel 1).</summary>
public sealed record ResultadoValidacion
{
    /// <summary>true si no hubo errores.</summary>
    public bool EsValido => Errores.Count == 0;

    /// <summary>Errores encontrados.</summary>
    public IReadOnlyList<ErrorCpe> Errores { get; init; } = [];

    /// <summary>Advertencias (observaciones de SUNAT que no impiden el envío).</summary>
    public IReadOnlyList<ObservacionCpe> Advertencias { get; init; } = [];
}

/// <summary>Error de validación o de SUNAT, con su ubicación.</summary>
public sealed record ErrorCpe
{
    /// <summary>Código SUNAT (p. ej. 2017) o código propio de PasheCPE (PCPE-xxxx).</summary>
    public required string Codigo { get; init; }

    /// <summary>Descripción del error.</summary>
    public required string Descripcion { get; init; }

    /// <summary>Ubicación: propiedad del modelo o ruta XPath del XML.</summary>
    public string? Ruta { get; init; }

    /// <summary>Capa que detectó el error.</summary>
    public OrigenError Origen { get; init; }

    /// <summary>Número del catálogo SUNAT involucrado, si aplica.</summary>
    public string? Catalogo { get; init; }
}

/// <summary>Capa que detectó un error.</summary>
public enum OrigenError
{
    /// <summary>Validación del modelo en C#.</summary>
    Modelo,

    /// <summary>Validación contra los XSD oficiales.</summary>
    Esquema,

    /// <summary>Reglas de las XSL oficiales de SUNAT.</summary>
    XslSunat,

    /// <summary>
    /// Reglas complementarias en C#: filas del Excel de validación de SUNAT que las XSL (06/09/2022) no tienen o
    /// calculan distinto, incluidas las vigentes desde 2027.
    /// </summary>
    ReglasComplementarias,

    /// <summary>Respuesta de SUNAT u OSE.</summary>
    Sunat,
}

/// <summary>Observación o advertencia con código y descripción.</summary>
public sealed record ObservacionCpe
{
    /// <summary>Código (p. ej. 4252 o PCPE-1102).</summary>
    public required string Codigo { get; init; }

    /// <summary>Descripción.</summary>
    public required string Descripcion { get; init; }
}

/// <summary>Rutas de los archivos de un documento en el almacén CPE.</summary>
public sealed record RutasCpe
{
    /// <summary>Sin rutas.</summary>
    public static RutasCpe Vacio { get; } = new();

    /// <summary>XML sin firmar (DATA).</summary>
    public string? Xml { get; init; }

    /// <summary>XML firmado (FIRMA).</summary>
    public string? XmlFirmado { get; init; }

    /// <summary>ZIP de envío (ENVIO).</summary>
    public string? Zip { get; init; }

    /// <summary>CDR (RPTA).</summary>
    public string? Cdr { get; init; }

    /// <summary>PDF (REPO).</summary>
    public string? Pdf { get; init; }

    /// <summary>Imagen del QR (QR).</summary>
    public string? Qr { get; init; }

    /// <summary>Historial JSON (LOG).</summary>
    public string? Historial { get; init; }
}
