using System.Globalization;
using PasheCPE.Tributos;
using PasheCPE.Ubl;
using static PasheCPE.Mapeo.MapeadorComun;

namespace PasheCPE.Mapeo;

/// <summary>
/// Cálculo de factura o boleta → Invoice UBL 2.1 del perfil SUNAT. No calcula nada: todos los importes vienen de
/// <see cref="CalculadoraTributaria"/>. Deja <c>ext:UBLExtensions</c> vacío, preparado para la firma.
/// </summary>
internal sealed class MapeadorInvoice(MapeadorComun comun)
{
    public Invoice Mapear(CalculoComprobante calculo)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        var cpe = (ComprobanteCpe)calculo.Comprobante;
        var emisor = calculo.Emisor;
        var moneda = cpe.Moneda;

        var invoice = new Invoice
        {
            UBLExtensions = ExtensionesVacias(),
            UBLVersionID = "2.1",
            CustomizationID = AtributosCatalogo.Personalizacion("2.0"),
            ID = SerieNumero(cpe.Serie, cpe.Numero),
            IssueDate = Fecha(calculo.FechaEmision),
            IssueTime = Hora(calculo.HoraEmision),
            DueDate = cpe.FechaVencimiento is { } vencimiento ? Fecha(vencimiento) : null,
            InvoiceTypeCode = AtributosCatalogo.TipoDocumento(cpe.TipoDocumento, cpe.TipoOperacion),
            DocumentCurrencyCode = AtributosCatalogo.Moneda(moneda),
            LineCountNumeric = new NumericType { Valor = calculo.Lineas.Count },
            OrderReference = cpe.OrdenCompra is { Length: > 0 } orden ? new OrderReference { ID = Id(orden) } : null,
            AccountingSupplierParty = new PartyRole { Party = Emisor(emisor) },
            AccountingCustomerParty = new PartyRole { Party = Cliente(cpe.Cliente) },
            LegalMonetaryTotal = Totales(calculo, moneda),
        };

        foreach (var leyenda in Leyendas(calculo))
        {
            invoice.Note.Add(leyenda);
        }

        Relacionados(cpe, invoice.DespatchDocumentReference, invoice.AdditionalDocumentReference);
        if (cpe.ContratoColaboracion is { } contrato)
        {
            invoice.ContractDocumentReference.Add(Contrato(contrato));
        }

        foreach (var anticipo in calculo.Anticipos)
        {
            var identificador = anticipo.Identificador.ToString(CultureInfo.InvariantCulture);
            invoice.AdditionalDocumentReference.Add(new DocumentReference
            {
                ID = Id(anticipo.Anticipo.SerieNumero),
                DocumentTypeCode = AtributosCatalogo.DocumentoRelacionado(anticipo.Anticipo.TipoDocumento),
                DocumentStatusCode = AtributosCatalogo.IdentificadorAnticipo(identificador),
                IssuerParty = new Party { PartyIdentification = { new PartyIdentification { ID = AtributosCatalogo.DocumentoIdentidad(TipoDocumentoIdentidad.Ruc, emisor.Ruc) } } },
            });
            invoice.PrepaidPayment.Add(new Payment { ID = AtributosCatalogo.PagoAnticipo(identificador), PaidAmount = Monto(anticipo.Anticipo.Monto, moneda) });
        }

        invoice.Signature.Add(comun.Firma(emisor));
        Pagos(invoice, calculo, moneda);

        foreach (var cargo in calculo.CargosGlobales)
        {
            invoice.AllowanceCharge.Add(Cargo(cargo, moneda));
        }

        invoice.TaxTotal.Add(comun.Tributos(calculo, moneda));
        foreach (var linea in calculo.Lineas)
        {
            invoice.InvoiceLine.Add(Linea(comun.Linea(linea, moneda)));
        }

        return invoice;
    }

    private static void Pagos(Invoice invoice, CalculoComprobante calculo, string moneda)
    {
        var cpe = calculo.Comprobante;
        if (calculo.Detraccion is { } detraccion)
        {
            invoice.PaymentMeans.Add(new PaymentMeans
            {
                ID = Id("Detraccion"),
                PaymentMeansCode = AtributosCatalogo.MedioPago(detraccion.Detraccion.MedioPago),
                PayeeFinancialAccount = new FinancialAccount { ID = Id(detraccion.Detraccion.CuentaBancoNacion) },
            });
            invoice.PaymentTerms.Add(new PaymentTerms
            {
                ID = Id("Detraccion"),
                PaymentMeansID = AtributosCatalogo.BienDetraccion(detraccion.Detraccion.CodigoBienServicio),
                PaymentPercent = new NumericType { Valor = detraccion.Detraccion.Porcentaje, Formato = FormatoUbl.Monto },
                Amount = Monto(detraccion.Monto, "PEN"),
            });
        }

        // La boleta al contado no informa la forma de pago.
        if (cpe.TipoDocumento != TipoDocumentoSunat.Factura && cpe.FormaPago is not { Tipo: TipoFormaPago.Credito })
        {
            return;
        }

        foreach (var termino in FormaPago(calculo, moneda))
        {
            invoice.PaymentTerms.Add(termino);
        }
    }

    private static AllowanceCharge Cargo(CargoCalculado cargo, string moneda) => new()
    {
        ChargeIndicator = cargo.EsCargo,
        AllowanceChargeReasonCode = AtributosCatalogo.CargoDescuento(cargo.Codigo),
        MultiplierFactorNumeric = new NumericType { Valor = cargo.Factor, Formato = "0.#####" },
        Amount = Monto(cargo.Monto, moneda),
        BaseAmount = Monto(cargo.Base, moneda),
    };

    private static InvoiceLine Linea(LineaUbl partes)
    {
        var linea = new InvoiceLine
        {
            ID = partes.Id,
            InvoicedQuantity = partes.Cantidad,
            LineExtensionAmount = partes.ValorVenta,
            PricingReference = partes.PrecioReferencia,
            Item = partes.Articulo,
            Price = partes.Precio,
        };
        if (partes.Descuento is { } descuento)
        {
            linea.AllowanceCharge.Add(descuento);
        }

        linea.TaxTotal.Add(partes.Tributos);
        return linea;
    }
}
