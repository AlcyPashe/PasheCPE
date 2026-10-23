using System.Xml.Linq;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using static PasheCPE.Tests.Utilidades.XmlDePrueba;

namespace PasheCPE.Tests.Validacion;

/// <summary>
/// Alteraciones de un XML válido que SUNAT rechaza u observa: cada una debe producir el código del Excel de reglas
/// (CódigosRetorno) por la XSL de SUNAT o por una regla complementaria.
/// </summary>
public sealed class ValidacionSunatInvalidosTests
{
    private static readonly Dictionary<string, (string Caso, string Codigo, bool EsError, Action<XElement> Alteracion)> _casos = new(StringComparer.Ordinal)
    {
        ["RucDistintoDelArchivo"] = ("Gravada18", "1034", true, r => r.Elemento(Cac + "AccountingSupplierParty", Cac + "Party", Cac + "PartyIdentification", Cbc + "ID").Value = "20987654326"),
        ["SerieDistintaDelArchivo"] = ("Gravada18", "1035", true, r => r.Element(Cbc + "ID")!.Value = "F002-123"),
        ["NumeroDistintoDelArchivo"] = ("Gravada18", "1036", true, r => r.Element(Cbc + "ID")!.Value = "F001-124"),
        ["TipoDistintoDelArchivo"] = ("Gravada18", "1003", true, r => r.Element(Cbc + "InvoiceTypeCode")!.Value = "03"),
        ["VersionUbl"] = ("Gravada18", "2074", true, r => r.Element(Cbc + "UBLVersionID")!.Value = "2.0"),
        ["VersionEstructura"] = ("Gravada18", "2072", true, r => r.Element(Cbc + "CustomizationID")!.Value = "1.0"),
        ["MonedaInexistente"] = ("Gravada18", "3088", true, r => r.Element(Cbc + "DocumentCurrencyCode")!.Value = "XYZ"),
        ["MonedaDeUnMontoDistinta"] = ("Gravada18", "2071", true, r => r.Elemento(Cac + "LegalMonetaryTotal", Cbc + "PayableAmount").SetAttributeValue("currencyID", "USD")),
        ["FacturaConClienteDni"] = ("Gravada18", "2800", true, r =>
        {
            var id = r.Elemento(Cac + "AccountingCustomerParty", Cac + "Party", Cac + "PartyIdentification", Cbc + "ID");
            id.SetAttributeValue("schemeID", "1");
            id.Value = "12345678";
        }),
        ["TipoOperacionNoAdmitidaEnFactura"] = ("Gravada18", "3206", true, r => r.Element(Cbc + "InvoiceTypeCode")!.SetAttributeValue("listID", "0113")),
        ["NombreDelTributo"] = ("Gravada18", "3051", true, r => r.PrimeraLinea().Subtotal("1000").Elemento(Cac + "TaxCategory", Cac + "TaxScheme", Cbc + "Name").Value = "IVA"),
        ["CodigoInternacionalDelTributo"] = ("Gravada18", "2377", true, r => r.PrimeraLinea().Subtotal("1000").Elemento(Cac + "TaxCategory", Cac + "TaxScheme", Cbc + "TaxTypeCode").Value = "FRE"),
        ["IdiomaDeLeyendaInexistente"] = ("Gravada18", "3027", true, r => r.Element(Cbc + "Note")!.SetAttributeValue("languageLocaleID", "9999")),
        ["LineaSinTributos"] = ("Gravada18", "3195", true, r => r.PrimeraLinea().Element(Cac + "TaxTotal")!.Remove()),
        ["TasaConSeisDecimales"] = ("Gravada18", "3102", true, r => r.PrimeraLinea().Subtotal("1000").Elemento(Cac + "TaxCategory", Cbc + "Percent").Value = "18.123456"),
        ["IgvDeLineaMalCalculado"] = ("Gravada18", "3103", true, r => r.PrimeraLinea().Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "25.00"),
        ["ValorUnitarioConOnceDecimales"] = ("Gravada18", "2369", true, r => r.PrimeraLinea().Elemento(Cac + "Price", Cbc + "PriceAmount").Value = "50.12345678901"),
        ["CodigoProductoDeTresDigitos"] = ("Gravada18", "4332", false, r => r.PrimeraLinea().Elemento(Cac + "Item", Cac + "CommodityClassification", Cbc + "ItemClassificationCode").Value = "123"),
        ["CodigoProductoConFormatoInvalido2026"] = ("Gravada18", "3496", false, r => r.PrimeraLinea().Elemento(Cac + "Item", Cac + "CommodityClassification", Cbc + "ItemClassificationCode").Value = "5022120A"),
        ["SumatoriaIgvFactura"] = ("Gravada18", "3291", true, r => r.Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "50.00"),
        ["TasasDistintasEnLineas"] = ("Gravada18", "3462", true, r => r.PrimeraLinea().Subtotal("1000").Elemento(Cac + "TaxCategory", Cbc + "Percent").Value = "10.50"),
        ["TotalPrecioVentaFactura"] = ("Gravada18", "3279", true, r => r.Elemento(Cac + "LegalMonetaryTotal", Cbc + "TaxInclusiveAmount").Value = "200.00"),
        ["SumatoriaIgvBoletaMype"] = ("BoletaMypeDiezYMedio", "4290", false, r => r.Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "30.00"),
        ["TotalPrecioVentaBoleta"] = ("BoletaMypeDiezYMedio", "4310", false, r => r.Elemento(Cac + "LegalMonetaryTotal", Cbc + "TaxInclusiveAmount").Value = "300.00"),
        ["ContratoConTipoInvalido"] = ("ContratoColaboracion2027", "3497", true, r => r.Elemento(Cac + "ContractDocumentReference", Cbc + "DocumentTypeCode").Value = "3"),
        ["ContratoSinDescripcion"] = ("ContratoColaboracion2027", "3499", true, r => r.Elemento(Cac + "ContractDocumentReference", Cbc + "DocumentDescription").Remove()),
        ["ContratoConPorcentajeInvalido"] = ("ContratoColaboracion2027", "3500", true, r => r.Element(Cac + "ContractDocumentReference")!.Descendants(Cbc + "PartecipationPercent").Single().Value = "150.5"),
        ["ContratoConNumeroLargo"] = ("ContratoColaboracion2027", "3501", true, r => r.Elemento(Cac + "ContractDocumentReference", Cbc + "ID").Value = new string('A', 51)),
        ["ContratoConTabulacion"] = ("ContratoColaboracion2027", "3502", true, r => r.Elemento(Cac + "ContractDocumentReference", Cbc + "DocumentDescription").Value = "CONSORCIO\tVIAL"),
        ["CodigoProductoConFormatoInvalido2027"] = ("ContratoColaboracion2027", "3496", true, r => r.PrimeraLinea().Elemento(Cac + "Item", Cac + "CommodityClassification", Cbc + "ItemClassificationCode").Value = "5022120A"),
    };

    public static TheoryData<string> Casos => [.. _casos.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Validar_XmlAlterado_DevuelveElCodigoDeSunat(string nombre)
    {
        var (caso, codigo, esError, alteracion) = _casos[nombre];
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(CasosTributarios.Todos[caso].FechaEmision));
        var (xml, referencia) = await GenerarAsync(servicios, caso);

        var resultado = servicios.Obtener<ValidadorSunat>().Validar(Alterar(xml, alteracion), referencia);

        var codigos = esError ? resultado.Errores.Select(e => e.Codigo) : resultado.Observaciones.Select(o => o.Codigo);
        Assert.True(
            codigos.Contains(codigo),
            $"Se esperaba el {(esError ? "error" : "la observación")} {codigo}. Errores: {string.Join(", ", resultado.Errores.Select(e => e.Codigo))}. Observaciones: {string.Join(", ", resultado.Observaciones.Select(o => o.Codigo))}.");
    }
}
