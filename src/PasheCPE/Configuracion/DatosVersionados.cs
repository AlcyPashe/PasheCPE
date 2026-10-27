namespace PasheCPE;

// Tipos de los datos versionados (tasas, plazos, endpoints). Se usan tanto para leer los JSON embebidos como para
// sobrescribirlos desde la configuración, por eso son clases con setters.

/// <summary>Tramo con vigencia: aplica desde <see cref="Desde"/> hasta <see cref="Hasta"/> (inclusive) o hasta el siguiente.</summary>
public abstract class TramoVigencia
{
    /// <summary>Primer día de vigencia.</summary>
    public DateOnly Desde { get; set; }

    /// <summary>Último día de vigencia; null = hasta que empiece el siguiente tramo.</summary>
    public DateOnly? Hasta { get; set; }
}

/// <summary>Tasa del IGV y su división en IGV e IPM.</summary>
public sealed class TramoIgv : TramoVigencia
{
    /// <summary>Tasa total en porcentaje (18.00).</summary>
    public decimal Tasa { get; set; }

    /// <summary>Parte del Impuesto General a las Ventas.</summary>
    public decimal Igv { get; set; }

    /// <summary>Parte del Impuesto de Promoción Municipal.</summary>
    public decimal Ipm { get; set; }
}

/// <summary>Tasa en porcentaje con vigencia.</summary>
public sealed class TramoTasa : TramoVigencia
{
    /// <summary>Tasa en porcentaje.</summary>
    public decimal Tasa { get; set; }
}

/// <summary>Monto del ICBPER por bolsa con vigencia.</summary>
public sealed class TramoIcbper : TramoVigencia
{
    /// <summary>Monto por bolsa plástica.</summary>
    public decimal MontoPorUnidad { get; set; }
}

/// <summary>
/// Vigencia que reemplaza la de <c>catalogos.json</c> (por ejemplo, si SUNAT posterga un código). Sin
/// <see cref="Codigo"/> se aplica al catálogo completo. Los valores informados reemplazan a los embebidos; null = sin
/// límite.
/// </summary>
public sealed class VigenciaCatalogo
{
    /// <summary>Número del catálogo (01, 10, 25.1…).</summary>
    public string Catalogo { get; set; } = string.Empty;

    /// <summary>Código del catálogo; null = todo el catálogo.</summary>
    public string? Codigo { get; set; }

    /// <summary>Primer día de vigencia; null = desde siempre.</summary>
    public DateOnly? Desde { get; set; }

    /// <summary>Último día de vigencia; null = sin fin.</summary>
    public DateOnly? Hasta { get; set; }
}

/// <summary>Plazo máximo de envío a SUNAT, en días calendario contados desde el día siguiente.</summary>
public sealed class PlazoEnvio
{
    /// <summary>Tipo de documento: 01, 03, 07, 08, RC o RA.</summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>Para notas: tipo del documento que modifican (01 o 03); null = cualquiera.</summary>
    public string? Modifica { get; set; }

    /// <summary>Días calendario.</summary>
    public int Dias { get; set; }

    /// <summary>Desde qué fecha se cuenta.</summary>
    public ContarPlazoDesde ContarDesde { get; set; } = ContarPlazoDesde.Emision;
}

/// <summary>Fecha desde la que se cuenta un plazo.</summary>
public enum ContarPlazoDesde
{
    /// <summary>Fecha de emisión del documento.</summary>
    Emision,

    /// <summary>Fecha de recepción del CDR del documento. Ningún plazo vigente la usa: la baja se cuenta desde la emisión (2957).</summary>
    RecepcionCdr,
}

/// <summary>Endpoint de un servicio por canal y ambiente.</summary>
public sealed class EndpointCpe
{
    /// <summary>Canal: «SUNAT» u «OSE:{nombre}» (<see cref="CanalEnvio.Clave"/>).</summary>
    public string Canal { get; set; } = string.Empty;

    /// <summary>Ambiente.</summary>
    public Ambiente Ambiente { get; set; }

    /// <summary>Servicio.</summary>
    public ServicioSunat Servicio { get; set; }

    /// <summary>URL del servicio.</summary>
#pragma warning disable CA1056 // La URL del token GRE lleva el marcador {client_id}; se maneja como texto.
    public string Url { get; set; } = string.Empty;
#pragma warning restore CA1056

    /// <summary>false si aún no se confirmó con un envío real (se advierte en el log la primera vez que se usa).</summary>
    public bool Verificado { get; set; } = true;

    /// <summary>
    /// Cabecera SOAPAction de los servicios SOAP; «{operacion}» se reemplaza por la operación (sendBill, getStatusCdr…).
    /// Vacía o null: <c>SOAPAction: ""</c>.
    /// </summary>
    public string? SoapAction { get; set; }
}

/// <summary>Servicio de SUNAT u OSE.</summary>
public enum ServicioSunat
{
    /// <summary>billService: sendBill, sendSummary y getStatus.</summary>
    Comprobantes,

    /// <summary>billConsultService: getStatusCdr.</summary>
    ConsultaCdr,

    /// <summary>Token OAuth2 de la API de guías.</summary>
    GreToken,

    /// <summary>API REST de guías.</summary>
    GreApi,
}
