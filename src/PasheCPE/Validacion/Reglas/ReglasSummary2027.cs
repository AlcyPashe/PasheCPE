using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.XPath;

namespace PasheCPE.Validacion.Reglas;

/// <summary>
/// Tasa del IGV en el resumen diario (desde el 01/01/2027): obligatoria en los tributos 1000 y 9996 (2992), de 10.5 o
/// 18 en boletas y de 10, 10.5 o 18 en notas (3504), con formato de hasta 3 enteros y 5 decimales (3102).
/// </summary>
internal sealed partial class ReglaTasaIgvResumen : IReglaCpe
{
    private static readonly decimal[] _tasasBoleta = [10.5m, 18m];
    private static readonly decimal[] _tasasNota = [10m, 10.5m, 18m];

    public string Nombre => "TasaIgvResumen";

    public IReadOnlyList<FilaExcel> Filas { get; } =
        [new("RC", 122), new("RC", 123), new("RC", 124), new("RC", 125), new("RC", 162), new("RC", 163), new("RC", 164)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        foreach (var linea in documento.Nodos("/*/sac:SummaryDocumentsLine"))
        {
            var esBoleta = documento.Texto("cbc:DocumentTypeCode", linea) == TipoDocumentoSunat.BoletaDeVenta;
            foreach (var subtotal in documento.Nodos("cac:TaxTotal/cac:TaxSubtotal", linea))
            {
                var tributo = documento.Texto("cac:TaxCategory/cac:TaxScheme/cbc:ID", subtotal);
                if (tributo is not (TributoSunat.Igv or TributoSunat.Gratuito))
                {
                    continue;
                }

                var gratuito = tributo == TributoSunat.Gratuito;
                var tasa = documento.Nodo("cac:TaxCategory/cbc:Percent", subtotal);
                if (tasa is null)
                {
                    yield return new HallazgoRegla(gratuito ? 162 : 122, DocumentoXml.Ruta(subtotal), $"Tributo {tributo} sin tasa.");
                    continue;
                }

                if (!Formato().IsMatch(tasa.Value.Trim()))
                {
                    yield return new HallazgoRegla(125, DocumentoXml.Ruta(tasa), $"Tasa '{tasa.Value}'.");
                    continue;
                }

                var valor = decimal.Parse(tasa.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
                if (!(esBoleta ? _tasasBoleta : _tasasNota).Contains(valor))
                {
                    var fila = (gratuito, esBoleta) switch
                    {
                        (false, true) => 123,
                        (false, false) => 124,
                        (true, true) => 163,
                        (true, false) => 164,
                    };
                    yield return new HallazgoRegla(fila, DocumentoXml.Ruta(tasa), $"Tasa {tasa.Value} en el tributo {tributo}.");
                }
            }
        }
    }

    /// <summary>Decimal positivo de hasta 3 enteros y 5 decimales.</summary>
    [GeneratedRegex(@"^\d{1,3}(\.\d{1,5})?$")]
    private static partial Regex Formato();
}

/// <summary>
/// IGV del resumen diario (4019, desde el 01/01/2027, observación): si es mayor que cero, el del tributo 1000 debe ser
/// (gravado + ISC) × tasa y el del 9996, las gratuitas gravadas (06) × tasa, con una tolerancia de ±5. Basta que cumpla
/// con alguna de las tasas vigentes: 18 o 10.5 en boletas, y además 10 en notas.
/// </summary>
internal sealed class ReglaIgvResumen : IReglaCpe
{
    private const decimal _tolerancia = 5m;
    private static readonly decimal[] _tasasBoleta = [18m, 10.5m];
    private static readonly decimal[] _tasasNota = [18m, 10.5m, 10m];

    public string Nombre => "IgvResumen";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("RC", 118), new("RC", 119), new("RC", 158), new("RC", 159)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        foreach (var linea in documento.Nodos("/*/sac:SummaryDocumentsLine"))
        {
            var esBoleta = documento.Texto("cbc:DocumentTypeCode", linea) == TipoDocumentoSunat.BoletaDeVenta;
            var tasas = esBoleta ? _tasasBoleta : _tasasNota;
            var isc = Impuesto(documento, linea, TributoSunat.Isc)?.Monto ?? 0m;

            if (Impuesto(documento, linea, TributoSunat.Igv) is { Monto: > 0 } igv)
            {
                var gravado = Pago(documento, linea, TipoValorVenta.Gravado);
                if (!Cumple(igv.Monto, gravado + isc, tasas))
                {
                    yield return new HallazgoRegla(esBoleta ? 118 : 119, DocumentoXml.Ruta(igv.Nodo), $"IGV {igv.Monto} y (gravado {gravado} + ISC {isc}) × tasa.");
                }
            }

            if (Impuesto(documento, linea, TributoSunat.Gratuito) is { Monto: > 0 } gratuito)
            {
                var gratuitas = Pago(documento, linea, TipoValorVenta.GratuitasGravadas);
                if (!Cumple(gratuito.Monto, gratuitas, tasas))
                {
                    yield return new HallazgoRegla(esBoleta ? 158 : 159, DocumentoXml.Ruta(gratuito.Nodo), $"IGV de gratuitas {gratuito.Monto} y gratuitas gravadas {gratuitas} × tasa.");
                }
            }
        }
    }

    private static bool Cumple(decimal impuesto, decimal baseImponible, decimal[] tasas) =>
        tasas.Any(t => Math.Abs(impuesto - (baseImponible * t / 100m)) <= _tolerancia);

    private static (XPathNavigator Nodo, decimal Monto)? Impuesto(DocumentoXml documento, XPathNavigator linea, string tributo) =>
        documento.Nodo($"cac:TaxTotal[cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:ID='{tributo}']/cbc:TaxAmount", linea) is { } nodo
            ? (nodo, documento.Decimal(".", nodo) ?? 0m)
            : null;

    private static decimal Pago(DocumentoXml documento, XPathNavigator linea, string tipo) =>
        documento.Suma($"sac:BillingPayment[cbc:InstructionID='{tipo}']/cbc:PaidAmount", linea);
}

/// <summary>
/// Razón social del adquiriente en el resumen diario (2022; error desde el 01/01/2027): si se informa, de 3 a 250
/// caracteres, con espacios pero sin otros caracteres de espaciado (salto de línea, tabulación…), y no solo espacios.
/// </summary>
internal sealed class ReglaRazonSocialAdquiriente : IReglaCpe
{
    public string Nombre => "RazonSocialAdquiriente";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("RC", 48)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        foreach (var nombre in documento.Nodos("/*/sac:SummaryDocumentsLine/cac:AccountingCustomerParty/cac:Party/cac:PartyLegalEntity/cbc:RegistrationName"))
        {
            var texto = nombre.Value;
            if (texto.Length is < 3 or > 250 || string.IsNullOrWhiteSpace(texto) || texto.Any(c => char.IsWhiteSpace(c) && c != ' '))
            {
                yield return new HallazgoRegla(48, DocumentoXml.Ruta(nombre), $"Razón social de {texto.Length} caracteres.");
            }
        }
    }
}

/// <summary>Nombre del tributo 9996 (IGV de gratuitas) en el resumen diario: «GRA» (3051, desde el 01/01/2027).</summary>
internal sealed class ReglaNombreTributoGratuito : IReglaCpe
{
    public string Nombre => "NombreTributoGratuito";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("RC", 169)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        foreach (var nombre in documento.Nodos($"/*/sac:SummaryDocumentsLine/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme[cbc:ID='{TributoSunat.Gratuito}']/cbc:Name"))
        {
            if (nombre.Value != "GRA")
            {
                yield return new HallazgoRegla(169, DocumentoXml.Ruta(nombre), $"Nombre '{nombre.Value}'.");
            }
        }
    }
}
