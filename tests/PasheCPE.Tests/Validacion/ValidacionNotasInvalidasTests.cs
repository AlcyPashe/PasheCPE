using System.Xml.Linq;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using static PasheCPE.Tests.Utilidades.XmlDePrueba;

namespace PasheCPE.Tests.Validacion;

/// <summary>
/// Alteraciones de una nota de crédito o débito válida que SUNAT rechaza u observa: cada una debe producir el código del
/// Excel de reglas por la XSL de SUNAT o por una regla complementaria (incluidas las de la R.S. 000048-2026).
/// </summary>
public sealed class ValidacionNotasInvalidasTests
{
    private static readonly Dictionary<string, (string Caso, string Codigo, bool EsError, Action<XElement> Alteracion)> _casos = new(StringComparer.Ordinal)
    {
        // Motivo y sustento
        ["MotivoInexistente"] = ("NcAnulacionFactura", "2172", true, r => r.Elemento(Cac + "DiscrepancyResponse", Cbc + "ResponseCode").Value = "99"),
        ["SustentoAusente"] = ("NcAnulacionFactura", "2136", true, r => r.Elemento(Cac + "DiscrepancyResponse", Cbc + "Description").Remove()),
        ["SustentoConSaltoDeLinea"] = ("NcAnulacionFactura", "2135", true, r => r.Elemento(Cac + "DiscrepancyResponse", Cbc + "Description").Value = "DEVOLUCIÓN\nPARCIAL"),
        ["MotivoDeDebitoInexistente"] = ("NdIntereses", "2172", true, r => r.Elemento(Cac + "DiscrepancyResponse", Cbc + "ResponseCode").Value = "07"),

        // Un único documento modificado (desde 2027)
        ["NotaDeCreditoConDosReferencias2027"] = ("NcContratoColaboracion2027", "3261", true, r => r.Element(Cac + "BillingReference")!.AddAfterSelf(new XElement(r.Element(Cac + "BillingReference")!))),
        ["NotaDeDebitoConDosReferencias2027"] = ("NdIntereses2027", "3194", true, r => r.Element(Cac + "BillingReference")!.AddAfterSelf(new XElement(r.Element(Cac + "BillingReference")!))),
        ["AjusteDeCuotasConDosReferencias"] = ("NcAjusteCuotas", "3261", true, r => r.Element(Cac + "BillingReference")!.AddAfterSelf(new XElement(r.Element(Cac + "BillingReference")!))),

        // Documento modificado obligatorio en la nota de débito (salvo penalidades desde 2027)
        ["NotaDeDebitoSinReferencia"] = ("NdIntereses", "2524", true, r => r.Element(Cac + "BillingReference")!.Remove()),
        ["NotaDeDebitoSinReferencia2027"] = ("NdIntereses2027", "2524", true, r => r.Element(Cac + "BillingReference")!.Remove()),

        // Serie de la nota coherente con el documento modificado
        ["NotaDeCreditoFModificaBoleta"] = ("NcAnulacionFactura", "2117", true, r => r.DocumentoModificado().Element(Cbc + "DocumentTypeCode")!.Value = "03"),
        ["NotaDeCreditoBModificaFactura"] = ("NcBoleta", "2117", true, r => r.DocumentoModificado().Element(Cbc + "DocumentTypeCode")!.Value = "01"),
        ["NotaDeDebitoFModificaBoleta"] = ("NdIntereses", "2204", true, r => r.DocumentoModificado().Element(Cbc + "DocumentTypeCode")!.Value = "03"),
        ["NotaDeDebitoBModificaFactura"] = ("NdBoleta", "2400", true, r => r.DocumentoModificado().Element(Cbc + "DocumentTypeCode")!.Value = "01"),
        ["NotaDeDebitoConFormatoDeReferencia"] = ("NdIntereses", "2205", true, r => r.DocumentoModificado().Element(Cbc + "ID")!.Value = "X001-1"),

        // Penalidades sin IGV (desde 2027)
        ["PenalidadesConIgvEnLinea"] = ("NdPenalidades2027", "3507", true, r =>
        {
            var subtotal = r.PrimeraLinea().Subtotal("9998");
            subtotal.Elemento(Cac + "TaxCategory", Cac + "TaxScheme", Cbc + "ID").Value = "1000";
            subtotal.Element(Cbc + "TaxAmount")!.Value = "54.00";
        }),
        ["PenalidadesConSumatoriaIgv"] = ("NdPenalidades2027", "3507", true, r =>
        {
            var subtotal = r.Subtotal("9998");
            subtotal.Elemento(Cac + "TaxCategory", Cac + "TaxScheme", Cbc + "ID").Value = "1000";
            subtotal.Element(Cbc + "TaxAmount")!.Value = "54.00";
        }),

        // Tributos
        ["SumatoriaIgvNotaDeCreditoDeFactura"] = ("NcAnulacionFactura", "3291", true, r => r.Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "50.00"),
        ["SumatoriaIgvNotaDeCreditoDeBoleta"] = ("NcBoleta", "4290", false, r => r.Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "50.00"),
        ["SumatoriaIgvNotaDeDebito"] = ("NdIntereses", "3291", true, r => r.Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "50.00"),
        ["TasasDistintasEnNotaDeCredito"] = ("NcAnulacionFactura", "3462", true, r => r.PrimeraLinea().Subtotal("1000").Elemento(Cac + "TaxCategory", Cbc + "Percent").Value = "10.50"),

        // Código de producto y contrato de colaboración (desde 2027)
        ["CodigoProductoEnNotaDeCredito2027"] = ("NcContratoColaboracion2027", "3496", true, r => r.PrimeraLinea().Elemento(Cac + "Item", Cac + "CommodityClassification", Cbc + "ItemClassificationCode").Value = "5022120A"),
        ["CodigoProductoEnNotaDeDebito2027"] = ("NdIntereses2027", "3496", true, r => r.PrimeraLinea().Elemento(Cac + "Item", Cac + "CommodityClassification", Cbc + "ItemClassificationCode").Value = "5022120A"),
        ["ContratoConTipoInvalidoEnNotaDeCredito"] = ("NcContratoColaboracion2027", "3497", true, r => r.Elemento(Cac + "ContractDocumentReference", Cbc + "DocumentTypeCode").Value = "3"),
    };

    public static TheoryData<string> Casos => [.. _casos.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Validar_NotaAlterada_DevuelveElCodigoDeSunat(string nombre)
    {
        var (caso, codigo, esError, alteracion) = _casos[nombre];
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(CasosNotas.Todos[caso].FechaEmision));
        var (xml, referencia) = await GenerarNotaAsync(servicios, caso);

        var resultado = servicios.Obtener<ValidadorSunat>().Validar(Alterar(xml, alteracion), referencia);

        var codigos = esError ? resultado.Errores.Select(e => e.Codigo) : resultado.Observaciones.Select(o => o.Codigo);
        Assert.True(
            codigos.Contains(codigo),
            $"Se esperaba el {(esError ? "error" : "la observación")} {codigo}. Errores: {string.Join(", ", resultado.Errores.Select(e => e.Codigo))}. Observaciones: {string.Join(", ", resultado.Observaciones.Select(o => o.Codigo))}.");
    }

    [Fact]
    public async Task Validar_PenalidadesSinDocumento2027_NoExigeLaReferencia()
    {
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(new DateOnly(2027, 1, 15)));
        var (xml, referencia) = await GenerarNotaAsync(servicios, "NdPenalidadesSinDocumento2027");

        var resultado = servicios.Obtener<ValidadorSunat>().Validar(xml, referencia);

        Assert.Empty(resultado.Errores);
    }

    [Fact]
    public async Task Validar_NotaDeDebitoSinDocumento2026_ElErrorLoDaLaXsl()
    {
        // Antes de la vigencia de la R.S. 000048-2026 manda la XSL (2524 salvo el motivo 03).
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(new DateOnly(2026, 10, 22)));
        var (xml, referencia) = await GenerarNotaAsync(servicios, "NdIntereses");

        var resultado = servicios.Obtener<ValidadorSunat>().Validar(Alterar(xml, r => r.Element(Cac + "BillingReference")!.Remove()), referencia);

        Assert.Contains(resultado.Errores, e => e.Codigo == "2524" && e.Origen == OrigenError.XslSunat);
    }
}
