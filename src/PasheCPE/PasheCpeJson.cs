using System.Text.Json.Serialization;

namespace PasheCPE;

/// <summary>
/// Contexto de System.Text.Json para que el sistema de ventas guarde tal cual los documentos, resultados e historial
/// (camelCase, enums como texto, nulos omitidos). El emisor no se incluye porque contiene credenciales.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ComprobanteCpe))]
[JsonSerializable(typeof(NotaCpe))]
[JsonSerializable(typeof(ResumenDiarioCpe))]
[JsonSerializable(typeof(ComunicacionBajaCpe))]
[JsonSerializable(typeof(GuiaRemisionCpe))]
[JsonSerializable(typeof(ReferenciaCpe))]
[JsonSerializable(typeof(ResultadoGeneracion))]
[JsonSerializable(typeof(ResultadoEnvio))]
[JsonSerializable(typeof(ResultadoEmision))]
[JsonSerializable(typeof(ResultadoValidacion))]
[JsonSerializable(typeof(HistorialCpe))]
[JsonSerializable(typeof(EstadoConectividad))]
public sealed partial class PasheCpeJson : JsonSerializerContext;

/// <summary>Mismo contrato que <see cref="PasheCpeJson"/>, con sangría, para los archivos de historial.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(HistorialCpe))]
internal sealed partial class HistorialJson : JsonSerializerContext;
