using System.Globalization;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Ubl;

namespace PasheCPE.Mapeo;

/// <summary>Un resumen diario (RC) listo para mapear: su referencia (fecha de generación y correlativo) y sus líneas.</summary>
internal sealed record LoteResumen(EmisorCpe Emisor, ReferenciaCpe Referencia, DateOnly FechaReferencia, IReadOnlyList<LineaResumenCpe> Lineas);

/// <summary>
/// Lote del resumen diario → SummaryDocuments UBL 2.0 del perfil SUNAT 1.1. No calcula importes: los totales vienen de
/// la línea y la tasa del IGV ya está resuelta (<see cref="LineaResumenCpe.TasaIgv"/>). Un <c>BillingPayment</c> por
/// tipo de valor de venta distinto de cero; las gratuitas van en el código 05 o desagregadas (06–09) según la vigencia
/// del catálogo N.° 11 a la fecha de generación, que es la de recepción en SUNAT; con las gratuitas desagregadas se
/// informa también el IGV de las gratuitas (9996). El IGV (1000) se informa siempre, con su tasa; ISC e ICBPER, solo si
/// son mayores que cero. Deja <c>ext:UBLExtensions</c> vacío, preparado para la firma.
/// </summary>
internal sealed class MapeadorSummary(CatalogosSunat catalogos, IOptions<OpcionesPasheCpe> opciones)
{
    private const string _catalogoValorVenta = "11";
    private const string _columnaCodigoInternacional = "Código internacional";
    private const string _columnaNombre = "Nombre";

    public SummaryDocuments Mapear(LoteResumen lote)
    {
        ArgumentNullException.ThrowIfNull(lote);
        var emisor = lote.Emisor;
        var resumen = new SummaryDocuments
        {
            UBLExtensions = new UBLExtensions { UBLExtension = { new UBLExtension() } },
            UBLVersionID = "2.0",
            CustomizationID = new IdentifierType { Value = "1.1" },
            ID = new IdentifierType { Value = Identificador(lote.Referencia) },
            ReferenceDate = Fecha(lote.FechaReferencia),
            IssueDate = Fecha(lote.Referencia.Fecha),
            AccountingSupplierParty = Cuenta(TipoDocumentoIdentidad.Ruc, emisor.Ruc, emisor.RazonSocial),
        };
        resumen.Signature.Add(MapeadorComun.Firma(emisor, opciones.Value.IdFirma));

        var desagregadas = GratuitasDesagregadas(lote.Referencia.Fecha);
        for (var i = 0; i < lote.Lineas.Count; i++)
        {
            resumen.SummaryDocumentsLine.Add(Linea(i + 1, lote.Lineas[i], desagregadas));
        }

        return resumen;
    }

    /// <summary>cbc:ID del resumen: RC-yyyyMMdd-n (el nombre del archivo sin el RUC).</summary>
    public static string Identificador(ReferenciaCpe referencia)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        return referencia.NombreArchivo[(referencia.Ruc.Length + 1)..];
    }

    /// <summary>true si a la fecha rigen los códigos 06–09 del catálogo N.° 11 (gratuitas desagregadas, desde 2027).</summary>
    public bool GratuitasDesagregadas(DateOnly fecha) =>
        catalogos.Validar(_catalogoValorVenta, TipoValorVenta.GratuitasGravadas, fecha) is null;

    private SummaryDocumentsLine Linea(int numero, LineaResumenCpe linea, bool desagregadas)
    {
        var moneda = linea.Moneda;
        var resultado = new SummaryDocumentsLine
        {
            LineID = numero.ToString(CultureInfo.InvariantCulture),
            DocumentTypeCode = new CodeType { Value = linea.TipoDocumento },
            ID = new IdentifierType { Value = SerieNumero(linea.Serie, linea.Numero) },
            AccountingCustomerParty = Cuenta(linea.Cliente.TipoDocumento, linea.Cliente.NumeroDocumento, linea.Cliente.RazonSocial),
            BillingReference = linea.DocumentoModificado is { } modificado
                ? new BillingReference
                {
                    InvoiceDocumentReference = new DocumentReference
                    {
                        ID = new IdentifierType { Value = SerieNumero(modificado.Serie, modificado.Numero) },
                        DocumentTypeCode = new CodeType { Value = modificado.TipoDocumento },
                    },
                }
                : null,
            Status = new LineStatus { ConditionCode = linea.Estado },
            TotalAmount = Monto(linea.ImporteTotal, moneda),
        };

        Pago(resultado, TipoValorVenta.Gravado, linea.TotalGravado, moneda);
        Pago(resultado, TipoValorVenta.Exonerado, linea.TotalExonerado, moneda);
        Pago(resultado, TipoValorVenta.Inafecto, linea.TotalInafecto, moneda);
        Pago(resultado, TipoValorVenta.Exportacion, linea.TotalExportacion, moneda);
        if (desagregadas)
        {
            Pago(resultado, TipoValorVenta.GratuitasGravadas, linea.TotalGratuitoGravado, moneda);
            Pago(resultado, TipoValorVenta.GratuitasExoneradas, linea.TotalGratuitoExonerado, moneda);
            Pago(resultado, TipoValorVenta.GratuitasInafectas, linea.TotalGratuitoInafecto, moneda);
            Pago(resultado, TipoValorVenta.GratuitasExportacion, linea.TotalGratuitoExportacion, moneda);
        }
        else
        {
            Pago(resultado, TipoValorVenta.Gratuitas, linea.TotalGratuitoGravado + linea.TotalGratuitoExonerado + linea.TotalGratuitoInafecto + linea.TotalGratuitoExportacion, moneda);
        }

        if (linea.OtrosCargos > 0)
        {
            resultado.AllowanceCharge.Add(new AllowanceCharge { ChargeIndicator = true, Amount = Monto(linea.OtrosCargos, moneda) });
        }

        var tasa = linea.TasaIgv ?? throw new InvalidOperationException($"La tasa del IGV de {linea.Serie}-{linea.Numero} no está resuelta.");
        resultado.TaxTotal.Add(Tributo(TributoSunat.Igv, linea.Igv, tasa, moneda));
        if (linea.Isc > 0)
        {
            resultado.TaxTotal.Add(Tributo(TributoSunat.Isc, linea.Isc, null, moneda));
        }

        if (linea.Icbper > 0)
        {
            resultado.TaxTotal.Add(Tributo(TributoSunat.Icbper, linea.Icbper, null, moneda));
        }

        if (desagregadas && linea.IgvGratuitas > 0)
        {
            resultado.TaxTotal.Add(Tributo(TributoSunat.Gratuito, linea.IgvGratuitas, tasa, moneda));
        }

        return resultado;
    }

    private static void Pago(SummaryDocumentsLine linea, string tipo, decimal monto, string moneda)
    {
        if (monto > 0)
        {
            linea.BillingPayment.Add(new BillingPayment { PaidAmount = Monto(monto, moneda), InstructionID = tipo });
        }
    }

    /// <summary>Un tributo por TaxTotal, con el subtotal igual al total (2344) y la tasa en el IGV y en el 9996.</summary>
    private TaxTotal Tributo(string tributo, decimal monto, decimal? tasa, string moneda)
    {
        var total = new TaxTotal { TaxAmount = Monto(monto, moneda) };
        total.TaxSubtotal.Add(new TaxSubtotal
        {
            TaxAmount = Monto(monto, moneda),
            TaxCategory = new TaxCategory
            {
                Percent = tasa is { } porcentaje ? new NumericType { Valor = porcentaje, Formato = "0.00" } : null,
                TaxScheme = new TaxScheme
                {
                    ID = new IdentifierType { Value = tributo },
                    Name = catalogos.Extra(TributoSunat.NumeroCatalogo, tributo, _columnaNombre),
                    TaxTypeCode = catalogos.Extra(TributoSunat.NumeroCatalogo, tributo, _columnaCodigoInternacional),
                },
            },
        });
        return total;
    }

    private static AccountParty Cuenta(string tipoDocumento, string numeroDocumento, string? razonSocial)
    {
        var cuenta = new AccountParty { CustomerAssignedAccountID = numeroDocumento, AdditionalAccountID = tipoDocumento };
        if (!string.IsNullOrWhiteSpace(razonSocial))
        {
            cuenta.Party = new Party { PartyLegalEntity = { new PartyLegalEntity { RegistrationName = razonSocial } } };
        }

        return cuenta;
    }

    private static string SerieNumero(string serie, long numero) => string.Create(CultureInfo.InvariantCulture, $"{serie}-{numero}");

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static AmountType Monto(decimal valor, string moneda) => new() { CurrencyId = moneda, Valor = valor };
}
