using System.Text.Json.Serialization;

namespace PasheCPE.Pdf;

/// <summary>
/// Contexto de System.Text.Json para guardar el <see cref="ResultadoPdf"/> con el mismo contrato que
/// <see cref="PasheCpeJson"/> (camelCase, enums como texto, nulos omitidos).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ResultadoPdf))]
public sealed partial class PasheCpePdfJson : JsonSerializerContext;
