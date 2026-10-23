using System.Globalization;

namespace PasheCPE.Validacion.Reglas;

/// <summary>Consultas comunes de las reglas de tributos de facturas, boletas y notas.</summary>
internal static class ConsultasInvoice
{
    public const string TipoOperacionNrus = "0113";

    public const string SubtotalIgvCabecera = "/*/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:ID='1000']";
    public const string SubtotalesIgvLineas = DocumentoXml.Lineas + "/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:ID='1000']";

    /// <summary>Monto de descuentos (false) o cargos (true) globales con los códigos del catálogo N.° 53 indicados.</summary>
    public static decimal CargosGlobales(DocumentoXml documento, bool esCargo, params string[] codigos)
    {
        var condicion = string.Join(" or ", codigos.Select(c => $"cbc:AllowanceChargeReasonCode='{c}'"));
        var indicador = esCargo ? "true" : "false";
        return documento.Suma($"/*/cac:AllowanceCharge[cbc:ChargeIndicator='{indicador}'][{condicion}]/cbc:Amount");
    }

    /// <summary>Tasa del IGV de las líneas gravadas (la primera con base mayor que cero), en porcentaje.</summary>
    public static decimal TasaIgvLineas(DocumentoXml documento) =>
        documento.Nodos(SubtotalesIgvLineas)
            .Where(s => documento.Decimal("cbc:TaxableAmount", s) > 0)
            .Select(s => documento.Decimal("cac:TaxCategory/cbc:Percent", s))
            .FirstOrDefault(p => p is not null) ?? 0m;

    /// <summary>true si alguna línea está afecta al IVAP (tributo 1016 con base mayor que cero).</summary>
    public static bool EsIvap(DocumentoXml documento) =>
        documento.Nodos(DocumentoXml.Lineas + "/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:ID='1016']")
            .Any(s => documento.Decimal("cbc:TaxableAmount", s) > 0);

    public static decimal TributoCabecera(DocumentoXml documento, string tributo) =>
        documento.Suma($"/*/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:ID='{tributo}']/cbc:TaxAmount");

    public static bool FueraDeTolerancia(decimal valor, decimal calculado) => Math.Abs(valor - calculado) > 1m;

    public static string Texto(decimal valor) => valor.ToString("0.00###", CultureInfo.InvariantCulture);
}

/// <summary>
/// Sumatoria del IGV (Excel 13/02/2026): Σ «Monto base» de las líneas con tributo 1000 − descuentos globales 02 y 04
/// + cargos globales 49 − anticipos de ISC 20, por la tasa de las líneas (18 % o 10.5 %), con tolerancia ±1.
/// Factura: 3291 (la XSL la tiene comentada). Boleta: 4290 (la XSL usa 0.18 fijo y solo para 0113; se suprime).
/// Notas: 3291 si modifican una factura y 4290 en otro caso (las XSL de NC y ND la tienen comentada).
/// </summary>
internal sealed class ReglaSumatoriaIgv : IReglaCpe
{
    public string Nombre => "SumatoriaIgv";

    public IReadOnlyList<FilaExcel> Filas { get; } =
        [new("01", 351), new("03", 346), new("03", 349), new("07", 341), new("07", 343), new("08", 328), new("08", 330)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento.Nodo(ConsultasInvoice.SubtotalIgvCabecera + "/cbc:TaxAmount") is not { } nodo)
        {
            yield break;
        }

        var igv = documento.Decimal(ConsultasInvoice.SubtotalIgvCabecera + "/cbc:TaxAmount") ?? 0m;
        var modificaFactura = documento.TipoDocumentoModificado == TipoDocumentoSunat.Factura;
        int fila;
        if (documento.TipoDocumento == "01")
        {
            fila = 351;
        }
        else if (documento.TipoDocumento == TipoDocumentoSunat.NotaDeCredito)
        {
            fila = modificaFactura ? 341 : 343;
        }
        else if (documento.TipoDocumento == TipoDocumentoSunat.NotaDeDebito)
        {
            fila = modificaFactura ? 328 : 330;
        }
        else if (documento.TipoOperacion != ConsultasInvoice.TipoOperacionNrus)
        {
            fila = 346;
        }
        else if (igv > 0)
        {
            fila = 349;
        }
        else
        {
            yield break;
        }

        var bases = documento.Suma(ConsultasInvoice.SubtotalesIgvLineas + "/cbc:TaxableAmount");
        var descuentos = ConsultasInvoice.CargosGlobales(documento, esCargo: false, "02", "04");
        var cargos = ConsultasInvoice.CargosGlobales(documento, esCargo: true, "49");
        var anticiposIsc = ConsultasInvoice.CargosGlobales(documento, esCargo: false, "20");
        var tasa = ConsultasInvoice.TasaIgvLineas(documento);
        var calculado = (bases - descuentos + cargos - anticiposIsc) * tasa / 100m;
        if (ConsultasInvoice.FueraDeTolerancia(igv, calculado))
        {
            yield return new HallazgoRegla(fila, DocumentoXml.Ruta(nodo), $"IGV {ConsultasInvoice.Texto(igv)}; calculado {ConsultasInvoice.Texto(calculado)} con tasa {ConsultasInvoice.Texto(tasa)} %.");
        }
    }
}

/// <summary>
/// Todas las líneas afectas al IGV deben tener la misma tasa (3462, Excel 13/02/2026). En boletas, salvo 0113. En
/// notas, con una fila según modifiquen una factura o no.
/// </summary>
internal sealed class ReglaTasaIgvUnica : IReglaCpe
{
    private static readonly string[] _gratuitasGravadas = ["11", "12", "13", "14", "15", "16"];

    public string Nombre => "TasaIgvUnica";

    public IReadOnlyList<FilaExcel> Filas { get; } =
        [new("01", 352), new("03", 347), new("07", 342), new("07", 344), new("08", 329), new("08", 331)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento.TipoDocumento == "03" && documento.TipoOperacion == ConsultasInvoice.TipoOperacionNrus)
        {
            yield break;
        }

        var tasas = documento.Nodos(DocumentoXml.Lineas + "/cac:TaxTotal/cac:TaxSubtotal")
            .Where(s => documento.Decimal("cbc:TaxableAmount", s) > 0)
            .Where(s => documento.Texto("cac:TaxCategory/cac:TaxScheme/cbc:ID", s) switch
            {
                "1000" => true,
                "9996" => _gratuitasGravadas.Contains(documento.Texto("cac:TaxCategory/cbc:TaxExemptionReasonCode", s)),
                _ => false,
            })
            .Select(s => documento.Decimal("cac:TaxCategory/cbc:Percent", s))
            .Distinct()
            .ToList();
        if (tasas.Count > 1)
        {
            var ruta = documento.Nodo(ConsultasInvoice.SubtotalIgvCabecera + "/cbc:TaxAmount") is { } nodo ? DocumentoXml.Ruta(nodo) : null;
            var lista = string.Join(", ", tasas.Select(t => t is null ? "sin tasa" : ConsultasInvoice.Texto(t.Value) + " %"));
            var modificaFactura = documento.TipoDocumentoModificado == TipoDocumentoSunat.Factura;
            var fila = documento.TipoDocumento switch
            {
                "01" => 352,
                TipoDocumentoSunat.NotaDeCredito => modificaFactura ? 342 : 344,
                TipoDocumentoSunat.NotaDeDebito => modificaFactura ? 329 : 331,
                _ => 347,
            };
            yield return new HallazgoRegla(fila, ruta, $"Las líneas afectas al IGV tienen tasas distintas: {lista}.");
        }
    }
}

/// <summary>
/// Total precio de venta (<c>TaxInclusiveAmount</c>) sin IVAP (Excel 13/02/2026): valor de venta + ISC + anticipos de ISC
/// (20) + otros tributos + ICBPER + (Σ base de las líneas con tributo 1000 − descuentos 02 + cargos 49) × tasa de las
/// líneas, con tolerancia ±1. Factura: 3279; boleta (salvo 0113): 4310. La XSL solo valida la rama del IVAP.
/// </summary>
internal sealed class ReglaTotalPrecioVenta : IReglaCpe
{
    private const string _total = "/*/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount";

    public string Nombre => "TotalPrecioVenta";

    public IReadOnlyList<FilaExcel> Filas { get; } = [new("01", 420), new("03", 416)];

    public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        if (documento.Nodo(_total) is not { } nodo || ConsultasInvoice.EsIvap(documento)
            || (documento.TipoDocumento == "03" && documento.TipoOperacion == ConsultasInvoice.TipoOperacionNrus))
        {
            yield break;
        }

        var total = documento.Decimal(_total) ?? 0m;
        var valorVenta = documento.Decimal("/*/cac:LegalMonetaryTotal/cbc:LineExtensionAmount") ?? 0m;
        var tributos = ConsultasInvoice.TributoCabecera(documento, "2000")
            + ConsultasInvoice.TributoCabecera(documento, "9999")
            + ConsultasInvoice.TributoCabecera(documento, "7152");
        var anticiposIsc = ConsultasInvoice.CargosGlobales(documento, esCargo: false, "20");
        var baseIgv = documento.Suma(ConsultasInvoice.SubtotalesIgvLineas + "/cbc:TaxableAmount")
            - ConsultasInvoice.CargosGlobales(documento, esCargo: false, "02")
            + ConsultasInvoice.CargosGlobales(documento, esCargo: true, "49");
        var tasa = ConsultasInvoice.TasaIgvLineas(documento);
        var calculado = valorVenta + tributos + anticiposIsc + (baseIgv * tasa / 100m);
        if (ConsultasInvoice.FueraDeTolerancia(total, calculado))
        {
            yield return new HallazgoRegla(
                documento.TipoDocumento == "01" ? 420 : 416,
                DocumentoXml.Ruta(nodo),
                $"Total precio de venta {ConsultasInvoice.Texto(total)}; calculado {ConsultasInvoice.Texto(calculado)} con tasa {ConsultasInvoice.Texto(tasa)} %.");
        }
    }
}
