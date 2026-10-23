using PasheCPE.Tributos;
using PasheCPE.Ubl;
using static PasheCPE.Mapeo.MapeadorComun;

namespace PasheCPE.Mapeo;

/// <summary>
/// Cálculo de nota de crédito → CreditNote UBL 2.1 del perfil SUNAT: motivo (catálogo N.° 09) y sustento en
/// cac:DiscrepancyResponse y un único comprobante modificado en cac:BillingReference (R.S. 000048-2026). La forma de pago
/// solo se informa en el ajuste de cuotas (motivo 13). Deja <c>ext:UBLExtensions</c> vacío, preparado para la firma.
/// </summary>
internal sealed class MapeadorCreditNote(MapeadorComun comun)
{
    public CreditNote Mapear(CalculoComprobante calculo)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        var nota = (NotaCpe)calculo.Comprobante;
        var moneda = nota.Moneda;

        var creditNote = new CreditNote
        {
            UBLExtensions = ExtensionesVacias(),
            UBLVersionID = "2.1",
            CustomizationID = AtributosCatalogo.Personalizacion("2.0"),
            ID = SerieNumero(nota.Serie, nota.Numero),
            IssueDate = Fecha(calculo.FechaEmision),
            IssueTime = Hora(calculo.HoraEmision),
            DocumentCurrencyCode = AtributosCatalogo.Moneda(moneda),
            DiscrepancyResponse = { Motivo(nota) },
            OrderReference = nota.OrdenCompra is { Length: > 0 } orden ? new OrderReference { ID = Id(orden) } : null,
            AccountingSupplierParty = new PartyRole { Party = Emisor(calculo.Emisor) },
            AccountingCustomerParty = new PartyRole { Party = Cliente(nota.Cliente) },
            LegalMonetaryTotal = Totales(calculo, moneda),
        };

        foreach (var leyenda in Leyendas(calculo))
        {
            creditNote.Note.Add(leyenda);
        }

        if (Referencia(nota) is { } referencia)
        {
            creditNote.BillingReference.Add(referencia);
        }

        Relacionados(nota, creditNote.DespatchDocumentReference, creditNote.AdditionalDocumentReference);
        if (nota.ContratoColaboracion is { } contrato)
        {
            creditNote.ContractDocumentReference.Add(Contrato(contrato));
        }

        creditNote.Signature.Add(comun.Firma(calculo.Emisor));
        if (nota.Motivo == MotivoNotaCredito.CorreccionMontoNetoPendientePago)
        {
            foreach (var termino in FormaPago(calculo, moneda))
            {
                creditNote.PaymentTerms.Add(termino);
            }
        }

        creditNote.TaxTotal.Add(comun.Tributos(calculo, moneda));
        foreach (var linea in calculo.Lineas)
        {
            var partes = comun.Linea(linea, moneda);
            creditNote.CreditNoteLine.Add(new CreditNoteLine
            {
                ID = partes.Id,
                CreditedQuantity = partes.Cantidad,
                LineExtensionAmount = partes.ValorVenta,
                PricingReference = partes.PrecioReferencia,
                TaxTotal = { partes.Tributos },
                Item = partes.Articulo,
                Price = partes.Precio,
            });
        }

        return creditNote;
    }

    /// <summary>cac:DiscrepancyResponse con el motivo (catálogo N.° 09 o N.° 10) y el sustento.</summary>
    internal static Response Motivo(NotaCpe nota) => new()
    {
        ResponseCode = AtributosCatalogo.MotivoNota(nota.TipoDocumento, nota.Motivo),
        Description = { nota.Sustento },
    };

    /// <summary>cac:BillingReference con el comprobante modificado; null si la nota no modifica ninguno (ND 13).</summary>
    internal static BillingReference? Referencia(NotaCpe nota) => nota.DocumentoModificado is { } modificado
        ? new BillingReference
        {
            InvoiceDocumentReference = new DocumentReference
            {
                ID = SerieNumero(modificado.Serie, modificado.Numero),
                DocumentTypeCode = AtributosCatalogo.DocumentoModificado(modificado.TipoDocumento),
            },
        }
        : null;
}
