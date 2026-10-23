using System.Globalization;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Tributos;
using PasheCPE.Ubl;

namespace PasheCPE.Mapeo;

/// <summary>
/// Partes UBL comunes a la factura, la boleta y las notas (emisor, cliente, firma, referencias, tributos, totales y
/// líneas). No calcula nada: todos los importes vienen de <see cref="CalculadoraTributaria"/>.
/// </summary>
internal sealed class MapeadorComun(CatalogosSunat catalogos, IOptions<OpcionesPasheCpe> opciones)
{
    private const string _columnaCodigoInternacional = "Código internacional";
    private const string _columnaNombre = "Nombre";

    public static IdentifierType Id(string valor) => new() { Value = valor };

    public static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Hora(TimeOnly hora) => hora.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static AmountType Monto(decimal valor, string moneda, string formato = FormatoUbl.Monto) =>
        new() { CurrencyId = moneda, Valor = valor, Formato = formato };

    /// <summary>ext:UBLExtensions vacío, preparado para la firma.</summary>
    public static UBLExtensions ExtensionesVacias() => new() { UBLExtension = { new UBLExtension() } };

    /// <summary>cbc:ID con serie-número.</summary>
    public static IdentifierType SerieNumero(string serie, long numero) => Id($"{serie}-{numero.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>cbc:Note: una por leyenda, con su código en languageLocaleID.</summary>
    public static IEnumerable<TextType> Leyendas(CalculoComprobante calculo) =>
        calculo.Leyendas.Select(l => new TextType { LanguageLocaleId = l.Codigo, Value = l.Texto });

    public static Party Emisor(EmisorCpe emisor)
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

    public static Party Cliente(ClienteCpe cliente)
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

    public Signature Firma(EmisorCpe emisor) => Firma(emisor, opciones.Value.IdFirma);

    /// <summary>cac:Signature: identifica al firmante y apunta a la firma <c>#IdFirma</c> (también lo usa el resumen diario).</summary>
    public static Signature Firma(EmisorCpe emisor, string idFirma)
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

    /// <summary>
    /// Documentos relacionados: las guías de remisión van a cac:DespatchDocumentReference y el resto a
    /// cac:AdditionalDocumentReference.
    /// </summary>
    public static void Relacionados(DocumentoVentaCpe documento, ICollection<DocumentReference> guias, ICollection<DocumentReference> adicionales)
    {
        foreach (var relacionado in documento.DocumentosRelacionados)
        {
            if (relacionado.Tipo is TipoDocumentoSunat.GuiaDeRemisionRemitente or TipoDocumentoSunat.GuiaDeRemisionTransportista)
            {
                guias.Add(new DocumentReference { ID = Id(relacionado.Numero), DocumentTypeCode = AtributosCatalogo.Guia(relacionado.Tipo) });
            }
            else
            {
                adicionales.Add(new DocumentReference { ID = Id(relacionado.Numero), DocumentTypeCode = AtributosCatalogo.DocumentoRelacionado(relacionado.Tipo) });
            }
        }
    }

    public static DocumentReference Contrato(ContratoColaboracionCpe contrato) => new()
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

    /// <summary>cac:PaymentTerms de la forma de pago: «Contado», o «Credito» con el monto neto pendiente y las cuotas.</summary>
    public static IEnumerable<PaymentTerms> FormaPago(CalculoComprobante calculo, string moneda)
    {
        var pago = calculo.Comprobante.FormaPago;
        if (pago is not { Tipo: TipoFormaPago.Credito })
        {
            yield return new PaymentTerms { ID = Id("FormaPago"), PaymentMeansID = Id("Contado") };
            yield break;
        }

        yield return new PaymentTerms { ID = Id("FormaPago"), PaymentMeansID = Id("Credito"), Amount = Monto(calculo.MontoNetoPendiente, moneda) };
        foreach (var (cuota, indice) in pago.Cuotas.Select((c, i) => (c, i)))
        {
            yield return new PaymentTerms
            {
                ID = Id("FormaPago"),
                PaymentMeansID = Id("Cuota" + (indice + 1).ToString("000", CultureInfo.InvariantCulture)),
                Amount = Monto(cuota.Monto, moneda),
                PaymentDueDate = Fecha(cuota.FechaVencimiento),
            };
        }
    }

    public TaxTotal Tributos(CalculoComprobante calculo, string moneda)
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

    public static MonetaryTotal Totales(CalculoComprobante calculo, string moneda) => new()
    {
        LineExtensionAmount = Monto(calculo.ValorVenta, moneda),
        TaxInclusiveAmount = Monto(calculo.PrecioVenta, moneda),
        AllowanceTotalAmount = calculo.DescuentosNoBase > 0 ? Monto(calculo.DescuentosNoBase, moneda) : null,
        ChargeTotalAmount = calculo.CargosNoBase > 0 ? Monto(calculo.CargosNoBase, moneda) : null,
        PrepaidAmount = calculo.TotalAnticipos > 0 ? Monto(calculo.TotalAnticipos, moneda) : null,
        PayableAmount = Monto(calculo.ImporteTotal, moneda),
    };

    /// <summary>Partes de una línea de detalle, comunes a InvoiceLine, CreditNoteLine y DebitNoteLine.</summary>
    public LineaUbl Linea(LineaCalculada calculada, string moneda)
    {
        var linea = calculada.Linea;
        var descuento = calculada.Descuento > 0
            ? new AllowanceCharge
            {
                ChargeIndicator = false,
                AllowanceChargeReasonCode = AtributosCatalogo.CargoDescuento(CargoDescuento.DescuentoLineaAfectaBase),
                MultiplierFactorNumeric = new NumericType { Valor = Math.Round(calculada.Descuento / calculada.ValorBruto, 5, MidpointRounding.AwayFromZero), Formato = "0.#####" },
                Amount = Monto(calculada.Descuento, moneda),
                BaseAmount = Monto(calculada.ValorBruto, moneda),
            }
            : null;

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

        return new LineaUbl(
            Id(calculada.Numero.ToString(CultureInfo.InvariantCulture)),
            AtributosCatalogo.Cantidad(linea.Cantidad, linea.Unidad),
            Monto(calculada.ValorVenta, moneda),
            new PricingReference
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
            descuento,
            tributos,
            Articulo(linea),
            new Price { PriceAmount = Monto(calculada.ValorUnitario, moneda, FormatoUbl.Unitario) });
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

/// <summary>Partes de una línea de detalle UBL; <see cref="Descuento"/> es null si la línea no tiene descuento.</summary>
internal sealed record LineaUbl(
    IdentifierType Id,
    QuantityType Cantidad,
    AmountType ValorVenta,
    PricingReference PrecioReferencia,
    AllowanceCharge? Descuento,
    TaxTotal Tributos,
    Item Articulo,
    Price Precio);
