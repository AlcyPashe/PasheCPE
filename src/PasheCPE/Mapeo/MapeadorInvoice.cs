using System.Globalization;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Tributos;
using PasheCPE.Ubl;

namespace PasheCPE.Mapeo;

/// <summary>
/// Cálculo de factura o boleta → Invoice UBL 2.1 del perfil SUNAT. No calcula nada: todos los importes vienen de
/// <see cref="CalculadoraTributaria"/>. Deja <c>ext:UBLExtensions</c> vacío, preparado para la firma.
/// </summary>
internal sealed class MapeadorInvoice(CatalogosSunat catalogos, IOptions<OpcionesPasheCpe> opciones)
{
    private const string _columnaCodigoInternacional = "Código internacional";
    private const string _columnaNombre = "Nombre";

    public Invoice Mapear(CalculoComprobante calculo)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        var cpe = calculo.Comprobante;
        var emisor = calculo.Emisor;
        var moneda = cpe.Moneda;

        var invoice = new Invoice
        {
            UBLExtensions = new UBLExtensions { UBLExtension = { new UBLExtension() } },
            UBLVersionID = "2.1",
            CustomizationID = AtributosCatalogo.Personalizacion("2.0"),
            ID = Id($"{cpe.Serie}-{cpe.Numero.ToString(CultureInfo.InvariantCulture)}"),
            IssueDate = Fecha(calculo.FechaEmision),
            IssueTime = calculo.HoraEmision.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            DueDate = cpe.FechaVencimiento is { } vencimiento ? Fecha(vencimiento) : null,
            InvoiceTypeCode = AtributosCatalogo.TipoDocumento(cpe.TipoDocumento, cpe.TipoOperacion),
            DocumentCurrencyCode = AtributosCatalogo.Moneda(moneda),
            LineCountNumeric = new NumericType { Valor = calculo.Lineas.Count },
            OrderReference = cpe.OrdenCompra is { Length: > 0 } orden ? new OrderReference { ID = Id(orden) } : null,
            AccountingSupplierParty = new PartyRole { Party = Emisor(emisor) },
            AccountingCustomerParty = new PartyRole { Party = Cliente(cpe.Cliente) },
            LegalMonetaryTotal = Totales(calculo, moneda),
        };

        foreach (var leyenda in calculo.Leyendas)
        {
            invoice.Note.Add(new TextType { LanguageLocaleId = leyenda.Codigo, Value = leyenda.Texto });
        }

        foreach (var relacionado in cpe.DocumentosRelacionados)
        {
            if (relacionado.Tipo is TipoDocumentoSunat.GuiaDeRemisionRemitente or TipoDocumentoSunat.GuiaDeRemisionTransportista)
            {
                invoice.DespatchDocumentReference.Add(new DocumentReference { ID = Id(relacionado.Numero), DocumentTypeCode = AtributosCatalogo.Guia(relacionado.Tipo) });
            }
            else
            {
                invoice.AdditionalDocumentReference.Add(new DocumentReference { ID = Id(relacionado.Numero), DocumentTypeCode = AtributosCatalogo.DocumentoRelacionado(relacionado.Tipo) });
            }
        }

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

        invoice.Signature.Add(Firma(emisor));
        Pagos(invoice, calculo, moneda);

        foreach (var cargo in calculo.CargosGlobales)
        {
            invoice.AllowanceCharge.Add(Cargo(cargo, moneda));
        }

        invoice.TaxTotal.Add(Tributos(calculo, moneda));
        foreach (var linea in calculo.Lineas)
        {
            invoice.InvoiceLine.Add(Linea(linea, moneda));
        }

        return invoice;
    }

    private static IdentifierType Id(string valor) => new() { Value = valor };

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static AmountType Monto(decimal valor, string moneda, string formato = FormatoUbl.Monto) =>
        new() { CurrencyId = moneda, Valor = valor, Formato = formato };

    private static Party Emisor(EmisorCpe emisor)
    {
        var domicilio = emisor.Domicilio;
        var party = new Party
        {
            PartyIdentification = { new PartyIdentification { ID = AtributosCatalogo.DocumentoIdentidad(TipoDocumentoIdentidad.Ruc, emisor.Ruc) } },
            PartyLegalEntity =
            {
                new PartyLegalEntity
                {
                    RegistrationName = emisor.RazonSocial,
                    RegistrationAddress = new Address
                    {
                        ID = domicilio.Ubigeo is { Length: > 0 } ubigeo ? AtributosCatalogo.Ubigeo(ubigeo) : null,
                        AddressTypeCode = AtributosCatalogo.Establecimiento(emisor.CodigoEstablecimiento),
                        CitySubdivisionName = Vacio(domicilio.Urbanizacion),
                        CityName = Vacio(domicilio.Provincia),
                        CountrySubentity = Vacio(domicilio.Departamento),
                        District = Vacio(domicilio.Distrito),
                        AddressLine = { new AddressLine { Line = domicilio.Direccion } },
                        Country = new Country { IdentificationCode = AtributosCatalogo.Pais(domicilio.CodigoPais) },
                    },
                },
            },
        };
        if (emisor.NombreComercial is { Length: > 0 } nombreComercial)
        {
            party.PartyName.Add(new PartyName { Name = nombreComercial });
        }

        return party;
    }

    private static Party Cliente(ClienteCpe cliente)
    {
        var entidad = new PartyLegalEntity { RegistrationName = cliente.RazonSocial };
        if (cliente.Direccion is { Direccion.Length: > 0 } direccion)
        {
            entidad.RegistrationAddress = new Address
            {
                ID = direccion.Ubigeo is { Length: > 0 } ubigeo ? AtributosCatalogo.Ubigeo(ubigeo) : null,
                AddressLine = { new AddressLine { Line = direccion.Direccion } },
                Country = new Country { IdentificationCode = AtributosCatalogo.Pais(direccion.CodigoPais) },
            };
        }

        return new Party
        {
            PartyIdentification = { new PartyIdentification { ID = AtributosCatalogo.DocumentoIdentidad(cliente.TipoDocumento, cliente.NumeroDocumento) } },
            PartyLegalEntity = { entidad },
        };
    }

    private Signature Firma(EmisorCpe emisor) => Firma(emisor, opciones.Value.IdFirma);

    /// <summary>cac:Signature: identifica al firmante y apunta a la firma <c>#IdFirma</c> (también lo usa el resumen diario).</summary>
    internal static Signature Firma(EmisorCpe emisor, string idFirma)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        return new Signature
        {
            ID = Id(idFirma),
            SignatoryParty = new Party
            {
                PartyIdentification = { new PartyIdentification { ID = Id(emisor.Ruc) } },
                PartyName = { new PartyName { Name = emisor.RazonSocial } },
            },
            DigitalSignatureAttachment = new Attachment { ExternalReference = new ExternalReference { URI = "#" + idFirma } },
        };
    }

    private static DocumentReference Contrato(ContratoColaboracionCpe contrato) => new()
    {
        ID = Id(contrato.Numero),
        DocumentTypeCode = new CodeType { Value = contrato.Tipo },
        DocumentDescription = contrato.Descripcion,
        IssuerParty = new Party
        {
            PartyLegalEntity =
            {
                new PartyLegalEntity
                {
                    ShareholderParty = { new ShareholderParty { PartecipationPercent = new NumericType { Valor = contrato.Porcentaje, Formato = FormatoUbl.Monto } } },
                },
            },
        },
    };

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

        var credito = cpe.FormaPago is { Tipo: TipoFormaPago.Credito };
        if (cpe.TipoDocumento != TipoDocumentoSunat.Factura && !credito)
        {
            return;
        }

        if (!credito)
        {
            invoice.PaymentTerms.Add(new PaymentTerms { ID = Id("FormaPago"), PaymentMeansID = Id("Contado") });
            return;
        }

        invoice.PaymentTerms.Add(new PaymentTerms { ID = Id("FormaPago"), PaymentMeansID = Id("Credito"), Amount = Monto(calculo.MontoNetoPendiente, moneda) });
        foreach (var (cuota, indice) in cpe.FormaPago!.Cuotas.Select((c, i) => (c, i)))
        {
            invoice.PaymentTerms.Add(new PaymentTerms
            {
                ID = Id("FormaPago"),
                PaymentMeansID = Id("Cuota" + (indice + 1).ToString("000", CultureInfo.InvariantCulture)),
                Amount = Monto(cuota.Monto, moneda),
                PaymentDueDate = Fecha(cuota.FechaVencimiento),
            });
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

    private TaxTotal Tributos(CalculoComprobante calculo, string moneda)
    {
        var total = new TaxTotal { TaxAmount = Monto(calculo.TotalTributos, moneda) };
        foreach (var subtotal in calculo.Subtotales)
        {
            total.TaxSubtotal.Add(new TaxSubtotal
            {
                TaxableAmount = subtotal.Tributo == TributoSunat.Icbper ? null : Monto(subtotal.Base, moneda),
                TaxAmount = Monto(subtotal.Monto, moneda),
                TaxCategory = new TaxCategory { TaxScheme = Esquema(subtotal.Tributo) },
            });
        }

        return total;
    }

    private static MonetaryTotal Totales(CalculoComprobante calculo, string moneda) => new()
    {
        LineExtensionAmount = Monto(calculo.ValorVenta, moneda),
        TaxInclusiveAmount = Monto(calculo.PrecioVenta, moneda),
        AllowanceTotalAmount = calculo.DescuentosNoBase > 0 ? Monto(calculo.DescuentosNoBase, moneda) : null,
        ChargeTotalAmount = calculo.CargosNoBase > 0 ? Monto(calculo.CargosNoBase, moneda) : null,
        PrepaidAmount = calculo.TotalAnticipos > 0 ? Monto(calculo.TotalAnticipos, moneda) : null,
        PayableAmount = Monto(calculo.ImporteTotal, moneda),
    };

    private InvoiceLine Linea(LineaCalculada calculada, string moneda)
    {
        var linea = calculada.Linea;
        var invoiceLine = new InvoiceLine
        {
            ID = Id(calculada.Numero.ToString(CultureInfo.InvariantCulture)),
            InvoicedQuantity = AtributosCatalogo.Cantidad(linea.Cantidad, linea.Unidad),
            LineExtensionAmount = Monto(calculada.ValorVenta, moneda),
            PricingReference = new PricingReference
            {
                AlternativeConditionPrice =
                {
                    new Price
                    {
                        PriceAmount = Monto(calculada.PrecioReferencia, moneda, FormatoUbl.Unitario),
                        PriceTypeCode = AtributosCatalogo.TipoPrecio(calculada.TipoPrecio),
                    },
                },
            },
            Item = Articulo(linea),
            Price = new Price { PriceAmount = Monto(calculada.ValorUnitario, moneda, FormatoUbl.Unitario) },
        };

        if (calculada.Descuento > 0)
        {
            invoiceLine.AllowanceCharge.Add(new AllowanceCharge
            {
                ChargeIndicator = false,
                AllowanceChargeReasonCode = AtributosCatalogo.CargoDescuento(CargoDescuento.DescuentoLineaAfectaBase),
                MultiplierFactorNumeric = new NumericType { Valor = Math.Round(calculada.Descuento / calculada.ValorBruto, 5, MidpointRounding.AwayFromZero), Formato = "0.#####" },
                Amount = Monto(calculada.Descuento, moneda),
                BaseAmount = Monto(calculada.ValorBruto, moneda),
            });
        }

        var tributos = new TaxTotal { TaxAmount = Monto(calculada.TotalTributos, moneda) };
        if (calculada.Isc > 0)
        {
            tributos.TaxSubtotal.Add(new TaxSubtotal
            {
                TaxableAmount = Monto(calculada.ValorVenta, moneda),
                TaxAmount = Monto(calculada.Isc, moneda),
                TaxCategory = new TaxCategory
                {
                    // SUNAT exige la tasa también en el sistema de monto fijo (2992) y valida ISC = base × tasa (3108):
                    // en ese sistema se informa la tasa equivalente.
                    Percent = linea.Isc?.Tasa is { } tasaIsc
                        ? new NumericType { Valor = tasaIsc, Formato = FormatoUbl.Monto }
                        : new NumericType { Valor = Math.Round(calculada.Isc / calculada.ValorVenta * 100m, 5, MidpointRounding.AwayFromZero), Formato = "0.#####" },
                    TierRange = linea.Isc?.Sistema,
                    TaxScheme = Esquema(TributoSunat.Isc),
                },
            });
        }

        tributos.TaxSubtotal.Add(new TaxSubtotal
        {
            TaxableAmount = Monto(calculada.ValorVenta + calculada.Isc, moneda),
            TaxAmount = Monto(calculada.Igv, moneda),
            TaxCategory = new TaxCategory
            {
                Percent = new NumericType { Valor = calculada.Tasa, Formato = FormatoUbl.Monto },
                TaxExemptionReasonCode = AtributosCatalogo.Afectacion(calculada.Afectacion),
                TaxScheme = Esquema(calculada.Tributo),
            },
        });

        if (calculada.Icbper > 0)
        {
            tributos.TaxSubtotal.Add(new TaxSubtotal
            {
                TaxAmount = Monto(calculada.Icbper, moneda),
                BaseUnitMeasure = new MeasureType { UnitCode = UnidadMedida.Unidad, Valor = linea.BolsasIcbper },
                TaxCategory = new TaxCategory { PerUnitAmount = Monto(calculada.TarifaIcbper, moneda), TaxScheme = Esquema(TributoSunat.Icbper) },
            });
        }

        invoiceLine.TaxTotal.Add(tributos);
        return invoiceLine;
    }

    private static Item Articulo(LineaCpe linea)
    {
        var item = new Item { Description = { linea.Descripcion } };
        if (linea.CodigoInterno is { Length: > 0 } codigo)
        {
            item.SellersItemIdentification = new ItemIdentification { ID = Id(codigo) };
        }

        if (linea.Gtin is { Length: > 0 } gtin)
        {
            item.StandardItemIdentification = new ItemIdentification { ID = AtributosCatalogo.Gtin(gtin) };
        }

        if (linea.CodigoProductoSunat is { Length: > 0 } producto)
        {
            item.CommodityClassification.Add(new CommodityClassification { ItemClassificationCode = AtributosCatalogo.ProductoSunat(producto) });
        }

        return item;
    }

    /// <summary>cac:TaxScheme con el nombre y el código internacional del catálogo N.° 05.</summary>
    private TaxScheme Esquema(string tributo) => new()
    {
        ID = AtributosCatalogo.Tributo(tributo),
        Name = catalogos.Extra(TributoSunat.NumeroCatalogo, tributo, _columnaNombre),
        TaxTypeCode = catalogos.Extra(TributoSunat.NumeroCatalogo, tributo, _columnaCodigoInternacional),
    };

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}
