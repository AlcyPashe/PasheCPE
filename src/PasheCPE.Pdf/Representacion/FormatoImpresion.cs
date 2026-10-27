using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PasheCPE.Catalogos;

namespace PasheCPE.Pdf.Representacion;

/// <summary>
/// Formatos de la representación impresa (PRD RF-12, TRD §22): montos <c>S/ 0,000.00</c> con el símbolo de
/// <c>monedas.json</c> (sin símbolo, el código ISO), fechas <c>DD/MM/AAAA</c> y cantidades sin ceros sobrantes. Los
/// títulos, las abreviaturas de documentos de identidad, el pie y el texto impreso de algunas leyendas son datos de
/// <c>textos-impresion.json</c>.
/// </summary>
internal static class FormatoImpresion
{
    private static readonly CultureInfo _cultura = CultureInfo.InvariantCulture;

    private static readonly Lazy<FrozenDictionary<string, string>> _simbolos = new(() =>
        (RecursosEmbebidos.Leer("monedas.json", DatosJson.Default.ArchivoMonedas).Simbolos ?? new Dictionary<string, string>())
            .ToFrozenDictionary(StringComparer.Ordinal));

    private static readonly Lazy<ArchivoTextosImpresion> _textos = new(() =>
    {
        using var flujo = typeof(FormatoImpresion).Assembly.GetManifestResourceStream("PasheCPE.Pdf.Recursos.textos-impresion.json")
            ?? throw new InvalidOperationException("Falta el recurso embebido 'textos-impresion.json' en PasheCPE.Pdf.");
        return JsonSerializer.Deserialize(flujo, TextosJson.Default.ArchivoTextosImpresion)
            ?? throw new InvalidDataException("El recurso 'textos-impresion.json' está vacío.");
    });

    /// <summary><c>S/ 1,234.56</c>, <c>US$ 10.00</c>; una moneda sin símbolo se muestra con su código.</summary>
    public static string Monto(decimal monto, string moneda) => $"{Simbolo(moneda)} {Numero(monto)}";

    /// <summary>Importe con separador de miles y 2 decimales, sin moneda: <c>1,234.56</c>.</summary>
    public static string Numero(decimal monto) => monto.ToString("#,##0.00", _cultura);

    /// <summary>Precio unitario: al menos 2 decimales y hasta 10 (como en el XML).</summary>
    public static string Precio(decimal precio) => precio.ToString("#,##0.00########", _cultura);

    /// <summary>Cantidad sin ceros sobrantes: <c>20</c>, <c>2.5</c>, <c>1,000.125</c>.</summary>
    public static string Cantidad(decimal cantidad) => cantidad.ToString("#,##0.##########", _cultura);

    public static string Porcentaje(decimal porcentaje) => porcentaje.ToString("0.##", _cultura) + " %";

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", _cultura);

    public static string Hora(TimeOnly hora) => hora.ToString("HH:mm:ss", _cultura);

    public static string Simbolo(string moneda) => _simbolos.Value.GetValueOrDefault(moneda) ?? moneda;

    /// <summary>Título del documento: «FACTURA ELECTRÓNICA»…</summary>
    public static string Titulo(string tipoDocumento) =>
        _textos.Value.Titulos.GetValueOrDefault(tipoDocumento) ?? throw new ArgumentException($"El tipo {tipoDocumento} no tiene título.", nameof(tipoDocumento));

    public static bool TieneDiseno(string tipoDocumento) => _textos.Value.Titulos.ContainsKey(tipoDocumento);

    /// <summary>Abreviatura del documento de identidad (RUC, DNI, C.E.…); un código desconocido se muestra tal cual.</summary>
    public static string DocumentoIdentidad(string tipo) => _textos.Value.DocumentosIdentidad.GetValueOrDefault(tipo) ?? tipo;

    /// <summary>Crédito al pie de cada página.</summary>
    public static string Pie => _textos.Value.Pie;

    /// <summary>
    /// Texto impreso de una leyenda: el de <c>textos-impresion.json</c> si lo tiene (las de la Amazonía, sin
    /// mayúsculas sostenidas); si no, el del XML. El XML firmado no cambia.
    /// </summary>
    public static string Leyenda(string codigo, string textoXml) => _textos.Value.Leyendas?.GetValueOrDefault(codigo) ?? textoXml;

    /// <summary>«Representación impresa de la FACTURA ELECTRÓNICA…».</summary>
    public static string RepresentacionImpresa(string tipoDocumento) =>
        string.Format(_cultura, _textos.Value.RepresentacionImpresa, Titulo(tipoDocumento));
}

internal sealed record ArchivoTextosImpresion(
    IReadOnlyDictionary<string, string> Titulos,
    IReadOnlyDictionary<string, string> DocumentosIdentidad,
    string RepresentacionImpresa,
    string Pie,
    IReadOnlyDictionary<string, string>? Leyendas);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ArchivoTextosImpresion))]
internal sealed partial class TextosJson : JsonSerializerContext;
