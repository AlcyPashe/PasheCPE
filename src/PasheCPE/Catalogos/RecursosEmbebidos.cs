using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PasheCPE.Catalogos;

/// <summary>Acceso a los datos versionados embebidos en <c>Recursos\</c> (TRD §3.3).</summary>
internal static class RecursosEmbebidos
{
    private const string _prefijo = "PasheCPE.Recursos.";

    public static Stream Abrir(string nombre) =>
        typeof(RecursosEmbebidos).Assembly.GetManifestResourceStream(_prefijo + nombre)
        ?? throw new InvalidOperationException($"Falta el recurso embebido '{nombre}' en PasheCPE.");

    public static T Leer<T>(string nombre, JsonTypeInfo<T> tipo)
    {
        using var flujo = Abrir(nombre);
        return JsonSerializer.Deserialize(flujo, tipo) ?? throw new InvalidDataException($"El recurso '{nombre}' está vacío.");
    }
}

/// <summary>Contrato de los JSON embebidos.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(ArchivoCatalogos))]
[JsonSerializable(typeof(ArchivoErrores))]
[JsonSerializable(typeof(ArchivoErroresLocales))]
[JsonSerializable(typeof(ArchivoTasas))]
[JsonSerializable(typeof(ArchivoPlazos))]
[JsonSerializable(typeof(ArchivoEndpoints))]
[JsonSerializable(typeof(ArchivoMonedas))]
[JsonSerializable(typeof(ArchivoParametros))]
[JsonSerializable(typeof(ArchivoCompatibilidadXsl))]
[JsonSerializable(typeof(ArchivoReglasCpe))]
[JsonSerializable(typeof(ArchivoAjustesXsl))]
[JsonSerializable(typeof(ArchivoEstadosConsultaCdr))]
[JsonSerializable(typeof(ArchivoMensajesUsuario))]
[JsonSerializable(typeof(ArchivoEstadosTicket))]
internal sealed partial class DatosJson : JsonSerializerContext;

internal sealed record ArchivoCatalogos(IReadOnlyDictionary<string, DatosCatalogo> Catalogos);

internal sealed record DatosCatalogo(
    string Nombre,
    bool SoloFormato,
    string? Formato,
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<ItemCatalogo> Items);

internal sealed record ItemCatalogo(
    string Codigo,
    string Descripcion,
    IReadOnlyDictionary<string, string>? Extras,
    DateOnly? Desde,
    DateOnly? Hasta);

internal sealed record ArchivoErrores(IReadOnlyList<DatosError> Errores);

/// <summary>
/// Código de retorno de SUNAT: tipo «servicio» (fault), «rechazo» u «observacion». <c>Transitorio</c>: falla interna de
/// SUNAT que se puede reintentar (máximo 3 veces); <c>EnProceso</c>: el documento ya está en proceso (0140), se consulta
/// el CDR y nunca se reenvía. <c>GrupoTransitorio</c> (solo transitorios): «disponibilidad» si SUNAT no recibió el
/// documento (se reenvía directamente) o «constancia» si pudo haberlo registrado (antes se consulta su estado).
/// </summary>
internal sealed record DatosError(
    string Codigo,
    string Descripcion,
    string Tipo,
    bool Transitorio,
    bool EnProceso,
    string? GrupoTransitorio,
    IReadOnlyList<string>? Condiciones,
    string? Listados);

internal sealed record ArchivoEstadosConsultaCdr(IReadOnlyList<EstadoConsultaCdr> Estados);

/// <summary>Estado que devuelve <c>getStatusCdr</c> (<c>statusCode</c>) y lo que significa para el comprobante.</summary>
internal sealed record EstadoConsultaCdr(string Codigo, string Descripcion, ResultadoConsultaCdr Resultado);

/// <summary>Significado de un estado de la consulta del CDR.</summary>
internal enum ResultadoConsultaCdr
{
    /// <summary>Existe y está aceptado.</summary>
    Aceptado,

    /// <summary>Existe y está rechazado.</summary>
    Rechazado,

    /// <summary>Existe y está dado de baja.</summary>
    Baja,

    /// <summary>SUNAT no lo tiene: se puede enviar.</summary>
    NoExiste,

    /// <summary>La consulta no es válida (formato, tipo de documento, pertenencia).</summary>
    Error,
}

/// <summary>Mensajes para el cajero: por estado y, con prioridad, por código.</summary>
internal sealed record ArchivoEstadosTicket(IReadOnlyList<EstadoTicket> Estados);

/// <summary>Estado del ticket que devuelve <c>getStatus</c> (<c>statusCode</c>).</summary>
internal sealed record EstadoTicket(string Codigo, string Descripcion, ResultadoTicket Resultado);

/// <summary>Significado de un estado del ticket.</summary>
internal enum ResultadoTicket
{
    /// <summary>Procesado: el CDR trae el resultado.</summary>
    Procesado,

    /// <summary>SUNAT aún lo procesa.</summary>
    EnProceso,

    /// <summary>Procesado con errores: rechazado (con CDR) o error del servicio (sin CDR).</summary>
    ConErrores,
}

internal sealed record ArchivoMensajesUsuario(IReadOnlyDictionary<string, string> Estados, IReadOnlyDictionary<string, string> Codigos);

internal sealed record ArchivoErroresLocales(IReadOnlyList<DatosErrorLocal> Errores);

internal sealed record DatosErrorLocal(string Codigo, string Descripcion);

internal sealed record ArchivoTasas(
    IReadOnlyList<TramoIgv> Igv,
    IReadOnlyList<TramoTasa> MypeRestaurantesHoteles,
    IReadOnlyList<TramoIcbper> Icbper);

internal sealed record ArchivoPlazos(IReadOnlyList<PlazoEnvio> Plazos);

internal sealed record ArchivoEndpoints(IReadOnlyList<EndpointCpe> Endpoints);

/// <summary>Nombres de moneda (monto en letras) y símbolos (representación impresa).</summary>
internal sealed record ArchivoMonedas(IReadOnlyDictionary<string, string> Monedas, IReadOnlyDictionary<string, string>? Simbolos);

internal sealed record ArchivoParametros(decimal BoletaSinIdentificacionHasta, DateOnly ContratoColaboracionDesde);

internal sealed record ArchivoCompatibilidadXsl(string Descripcion, IReadOnlyList<Validacion.Emulacion.VariableRespaldoXsl> VariablesRespaldo);

internal sealed record ArchivoReglasCpe(IReadOnlyList<ReglaExcelSunat> Reglas);

/// <summary>Fila de una hoja de reglas del Excel de SUNAT (01, 03, 07, 08, RC, RA).</summary>
internal sealed record ReglaExcelSunat(
    string Documento,
    int Fila,
    string? Dato,
    string? TagUbl,
    string? Validacion,
    string Tipo,
    string? TipoAnterior,
    string Codigo,
    string? Mensaje,
    DateOnly? Vigencia,
    string? Capa = null);

internal sealed record ArchivoAjustesXsl(
    string Descripcion,
    IReadOnlyList<Validacion.Reglas.SupresionXsl> Suprimidos,
    IReadOnlyList<Validacion.Reglas.ClasificacionRegla> Clasificacion);
