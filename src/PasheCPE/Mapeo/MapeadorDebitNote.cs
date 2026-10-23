using PasheCPE.Tributos;
using PasheCPE.Ubl;
using static PasheCPE.Mapeo.MapeadorComun;

namespace PasheCPE.Mapeo;

/// <summary>
/// Cálculo de nota de débito → DebitNote UBL 2.1 del perfil SUNAT: motivo (catálogo N.° 10) y sustento en
/// cac:DiscrepancyResponse y, salvo en las penalidades (motivo 13) sin comprobante, un único comprobante modificado en
/// cac:BillingReference (R.S. 000048-2026). Los totales van en cac:RequestedMonetaryTotal. Deja <c>ext:UBLExtensions</c>
/// vacío, preparado para la firma.
/// </summary>
internal sealed class MapeadorDebitNote(MapeadorComun comun)
{
    public DebitNote Mapear(CalculoComprobante calculo)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        var nota = (NotaCpe)calculo.Comprobante;
        var moneda = nota.Moneda;

        var debitNote = new DebitNote
        {
            UBLExtensions = ExtensionesVacias(),
            UBLVersionID = "2.1",
            CustomizationID = AtributosCatalogo.Personalizacion("2.0"),
            ID = SerieNumero(nota.Serie, nota.Numero),
            IssueDate = Fecha(calculo.FechaEmision),
            IssueTime = Hora(calculo.HoraEmision),
            DocumentCurrencyCode = AtributosCatalogo.Moneda(moneda),
            DiscrepancyResponse = { MapeadorCreditNote.Motivo(nota) },
            OrderReference = nota.OrdenCompra is { Length: > 0 } orden ? new OrderReference { ID = Id(orden) } : null,
            AccountingSupplierParty = new PartyRole { Party = Emisor(calculo.Emisor) },
            AccountingCustomerParty = new PartyRole { Party = Cliente(nota.Cliente) },
            RequestedMonetaryTotal = Totales(calculo, moneda),
        };

        foreach (var leyenda in Leyendas(calculo))
        {
            debitNote.Note.Add(leyenda);
        }

        if (MapeadorCreditNote.Referencia(nota) is { } referencia)
        {
            debitNote.BillingReference.Add(referencia);
        }

        Relacionados(nota, debitNote.DespatchDocumentReference, debitNote.AdditionalDocumentReference);
        if (nota.ContratoColaboracion is { } contrato)
        {
            debitNote.ContractDocumentReference.Add(Contrato(contrato));
        }

        debitNote.Signature.Add(comun.Firma(calculo.Emisor));
        debitNote.TaxTotal.Add(comun.Tributos(calculo, moneda));
        foreach (var linea in calculo.Lineas)
        {
            var partes = comun.Linea(linea, moneda);
            debitNote.DebitNoteLine.Add(new DebitNoteLine
            {
                ID = partes.Id,
                DebitedQuantity = partes.Cantidad,
                LineExtensionAmount = partes.ValorVenta,
                PricingReference = partes.PrecioReferencia,
                TaxTotal = { partes.Tributos },
                Item = partes.Articulo,
                Price = partes.Precio,
            });
        }

        return debitNote;
    }
}
