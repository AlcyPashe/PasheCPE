using System.Text.Json.Serialization;
using PasheCPE.Empaquetado;

namespace PasheCPE;

/// <summary>
/// Identifica un documento ya generado: resuelve su nombre de archivo y su periodo (año y mes de <see cref="Fecha"/>)
/// en el almacén. Para comprobantes y guías la fecha es la de emisión; para RC y RA, la de generación.
/// </summary>
public sealed record ReferenciaCpe
{
    /// <summary>Tipo de documento del resumen diario.</summary>
    public const string TipoResumenDiario = "RC";

    /// <summary>Tipo de documento de la comunicación de baja.</summary>
    public const string TipoComunicacionBaja = "RA";

    /// <summary>RUC del emisor.</summary>
    public required string Ruc { get; init; }

    /// <summary>01, 03, 07, 08, 09, 31, RC o RA.</summary>
    public required string TipoDocumento { get; init; }

    /// <summary>Serie (comprobantes y guías).</summary>
    public string? Serie { get; init; }

    /// <summary>Número (comprobantes y guías).</summary>
    public long? Numero { get; init; }

    /// <summary>Fecha de emisión (comprobantes y guías) o de generación (RC y RA).</summary>
    public required DateOnly Fecha { get; init; }

    /// <summary>Correlativo del día (RC y RA).</summary>
    public int? Correlativo { get; init; }

    /// <summary>Nombre oficial del archivo, sin extensión: RUC-TT-SERIE-NUMERO o RUC-RC-yyyyMMdd-n.</summary>
    [JsonIgnore]
    public string NombreArchivo => NombreArchivoCpe.Base(this);

    /// <summary>Comprobante (01, 03, 07, 08).</summary>
    public static ReferenciaCpe Comprobante(string ruc, string tipoDocumento, string serie, long numero, DateOnly fechaEmision) =>
        ConSerie(ruc, tipoDocumento, serie, numero, fechaEmision);

    /// <summary>Guía de remisión (09 o 31).</summary>
    public static ReferenciaCpe Guia(string ruc, string tipoDocumento, string serie, long numero, DateOnly fechaEmision) =>
        ConSerie(ruc, tipoDocumento, serie, numero, fechaEmision);

    /// <summary>Resumen diario (RC).</summary>
    public static ReferenciaCpe Resumen(string ruc, DateOnly fechaGeneracion, int correlativo) =>
        ConCorrelativo(ruc, TipoResumenDiario, fechaGeneracion, correlativo);

    /// <summary>Comunicación de baja (RA).</summary>
    public static ReferenciaCpe Baja(string ruc, DateOnly fechaGeneracion, int correlativo) =>
        ConCorrelativo(ruc, TipoComunicacionBaja, fechaGeneracion, correlativo);

    private static ReferenciaCpe ConSerie(string ruc, string tipoDocumento, string serie, long numero, DateOnly fecha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruc);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDocumento);
        ArgumentException.ThrowIfNullOrWhiteSpace(serie);
        return new ReferenciaCpe { Ruc = ruc, TipoDocumento = tipoDocumento, Serie = serie, Numero = numero, Fecha = fecha };
    }

    private static ReferenciaCpe ConCorrelativo(string ruc, string tipoDocumento, DateOnly fecha, int correlativo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruc);
        return new ReferenciaCpe { Ruc = ruc, TipoDocumento = tipoDocumento, Fecha = fecha, Correlativo = correlativo };
    }
}
