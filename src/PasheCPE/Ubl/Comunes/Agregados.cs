using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml;
using System.Xml.Serialization;

// Agregados UBL 2.1 del perfil SUNAT: solo los elementos que usan las guías de elaboración, con el nombre y el orden
// del XSD oficial. Públicas solo por XmlSerializer: NO forman parte del contrato estable de PasheCPE.
namespace PasheCPE.Ubl;

/// <summary>ext:UBLExtensions. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UBLExtensions
{
    /// <summary>ext:UBLExtension.</summary>
    [XmlElement("UBLExtension", Namespace = EspaciosUbl.Ext)]
    public Collection<UBLExtension> UBLExtension { get; } = [];
}

/// <summary>ext:UBLExtension. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class UBLExtension
{
    /// <summary>ext:ExtensionContent (aquí se inserta la firma).</summary>
    [XmlElement("ExtensionContent", Namespace = EspaciosUbl.Ext)]
    public ExtensionContent ExtensionContent { get; set; } = new();
}

/// <summary>ext:ExtensionContent. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ExtensionContent
{
    /// <summary>Contenido (ds:Signature después de firmar).</summary>
    [XmlAnyElement]
    public XmlElement? Any { get; set; }
}

/// <summary>cac:Signature. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Signature
{
    /// <summary>cbc:ID.</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cac:SignatoryParty.</summary>
    [XmlElement("SignatoryParty", Namespace = EspaciosUbl.Cac, Order = 1)]
    public Party? SignatoryParty { get; set; }

    /// <summary>cac:DigitalSignatureAttachment.</summary>
    [XmlElement("DigitalSignatureAttachment", Namespace = EspaciosUbl.Cac, Order = 2)]
    public Attachment? DigitalSignatureAttachment { get; set; }
}

/// <summary>cac:Attachment. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Attachment
{
    /// <summary>cac:ExternalReference.</summary>
    [XmlElement("ExternalReference", Namespace = EspaciosUbl.Cac)]
    public ExternalReference? ExternalReference { get; set; }
}

/// <summary>cac:ExternalReference. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ExternalReference
{
    /// <summary>cbc:URI («#IdFirma»).</summary>
    [XmlElement("URI", Namespace = EspaciosUbl.Cbc)]
#pragma warning disable CA1056 // Referencia interna «#IdFirma»; no es una URL.
    public string? URI { get; set; }
#pragma warning restore CA1056
}

/// <summary>cac:AccountingSupplierParty / cac:AccountingCustomerParty. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PartyRole
{
    /// <summary>cac:Party.</summary>
    [XmlElement("Party", Namespace = EspaciosUbl.Cac)]
    public Party? Party { get; set; }
}

/// <summary>cac:Party. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Party
{
    /// <summary>cac:PartyIdentification.</summary>
    [XmlElement("PartyIdentification", Namespace = EspaciosUbl.Cac, Order = 0)]
    public Collection<PartyIdentification> PartyIdentification { get; } = [];

    /// <summary>cac:PartyName.</summary>
    [XmlElement("PartyName", Namespace = EspaciosUbl.Cac, Order = 1)]
    public Collection<PartyName> PartyName { get; } = [];

    /// <summary>cac:PartyLegalEntity.</summary>
    [XmlElement("PartyLegalEntity", Namespace = EspaciosUbl.Cac, Order = 2)]
    public Collection<PartyLegalEntity> PartyLegalEntity { get; } = [];
}

/// <summary>cac:PartyIdentification. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PartyIdentification
{
    /// <summary>cbc:ID.</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc)]
    public IdentifierType? ID { get; set; }
}

/// <summary>cac:PartyName. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PartyName
{
    /// <summary>cbc:Name.</summary>
    [XmlElement("Name", Namespace = EspaciosUbl.Cbc)]
    public string? Name { get; set; }
}

/// <summary>cac:PartyLegalEntity. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PartyLegalEntity
{
    /// <summary>cbc:RegistrationName.</summary>
    [XmlElement("RegistrationName", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public string? RegistrationName { get; set; }

    /// <summary>cac:RegistrationAddress.</summary>
    [XmlElement("RegistrationAddress", Namespace = EspaciosUbl.Cac, Order = 1)]
    public Address? RegistrationAddress { get; set; }

    /// <summary>cac:ShareholderParty (contrato de colaboración empresarial).</summary>
    [XmlElement("ShareholderParty", Namespace = EspaciosUbl.Cac, Order = 2)]
    public Collection<ShareholderParty> ShareholderParty { get; } = [];
}

/// <summary>cac:ShareholderParty. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ShareholderParty
{
    /// <summary>cbc:PartecipationPercent (así lo escribe el estándar UBL).</summary>
    [XmlElement("PartecipationPercent", Namespace = EspaciosUbl.Cbc)]
    public NumericType? PartecipationPercent { get; set; }
}

/// <summary>cac:Address (RegistrationAddress). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Address
{
    /// <summary>cbc:ID (ubigeo).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:AddressTypeCode (código de establecimiento).</summary>
    [XmlElement("AddressTypeCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? AddressTypeCode { get; set; }

    /// <summary>cbc:CitySubdivisionName (urbanización).</summary>
    [XmlElement("CitySubdivisionName", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public string? CitySubdivisionName { get; set; }

    /// <summary>cbc:CityName (provincia).</summary>
    [XmlElement("CityName", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public string? CityName { get; set; }

    /// <summary>cbc:CountrySubentity (departamento).</summary>
    [XmlElement("CountrySubentity", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public string? CountrySubentity { get; set; }

    /// <summary>cbc:District (distrito).</summary>
    [XmlElement("District", Namespace = EspaciosUbl.Cbc, Order = 5)]
    public string? District { get; set; }

    /// <summary>cac:AddressLine.</summary>
    [XmlElement("AddressLine", Namespace = EspaciosUbl.Cac, Order = 6)]
    public Collection<AddressLine> AddressLine { get; } = [];

    /// <summary>cac:Country.</summary>
    [XmlElement("Country", Namespace = EspaciosUbl.Cac, Order = 7)]
    public Country? Country { get; set; }
}

/// <summary>cac:AddressLine. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AddressLine
{
    /// <summary>cbc:Line.</summary>
    [XmlElement("Line", Namespace = EspaciosUbl.Cbc)]
    public string? Line { get; set; }
}

/// <summary>cac:Country. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Country
{
    /// <summary>cbc:IdentificationCode.</summary>
    [XmlElement("IdentificationCode", Namespace = EspaciosUbl.Cbc)]
    public CodeType? IdentificationCode { get; set; }
}

/// <summary>cac:PaymentTerms (forma de pago, cuotas y detracción). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PaymentTerms
{
    /// <summary>cbc:ID («FormaPago» o «Detraccion»).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:PaymentMeansID (Contado, Credito, Cuota001… o el código de bien de la detracción).</summary>
    [XmlElement("PaymentMeansID", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public IdentifierType? PaymentMeansID { get; set; }

    /// <summary>cbc:PaymentPercent (porcentaje de la detracción).</summary>
    [XmlElement("PaymentPercent", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public NumericType? PaymentPercent { get; set; }

    /// <summary>cbc:Amount.</summary>
    [XmlElement("Amount", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public AmountType? Amount { get; set; }

    /// <summary>cbc:PaymentDueDate (yyyy-MM-dd).</summary>
    [XmlElement("PaymentDueDate", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public string? PaymentDueDate { get; set; }
}

/// <summary>cac:PaymentMeans (cuenta de la detracción). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PaymentMeans
{
    /// <summary>cbc:ID.</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:PaymentMeansCode (catálogo N.° 59).</summary>
    [XmlElement("PaymentMeansCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? PaymentMeansCode { get; set; }

    /// <summary>cac:PayeeFinancialAccount.</summary>
    [XmlElement("PayeeFinancialAccount", Namespace = EspaciosUbl.Cac, Order = 2)]
    public FinancialAccount? PayeeFinancialAccount { get; set; }
}

/// <summary>cac:FinancialAccount. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class FinancialAccount
{
    /// <summary>cbc:ID (número de cuenta).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc)]
    public IdentifierType? ID { get; set; }
}

/// <summary>cac:PrepaidPayment (anticipo). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Payment
{
    /// <summary>cbc:ID (identificador del pago).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:PaidAmount.</summary>
    [XmlElement("PaidAmount", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public AmountType? PaidAmount { get; set; }
}

/// <summary>cac:AllowanceCharge. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AllowanceCharge
{
    /// <summary>cbc:ChargeIndicator.</summary>
    [XmlElement("ChargeIndicator", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public bool ChargeIndicator { get; set; }

    /// <summary>cbc:AllowanceChargeReasonCode (catálogo N.° 53).</summary>
    [XmlElement("AllowanceChargeReasonCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? AllowanceChargeReasonCode { get; set; }

    /// <summary>cbc:MultiplierFactorNumeric.</summary>
    [XmlElement("MultiplierFactorNumeric", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public NumericType? MultiplierFactorNumeric { get; set; }

    /// <summary>cbc:Amount.</summary>
    [XmlElement("Amount", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public AmountType? Amount { get; set; }

    /// <summary>cbc:BaseAmount.</summary>
    [XmlElement("BaseAmount", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public AmountType? BaseAmount { get; set; }
}

/// <summary>cac:TaxTotal. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TaxTotal
{
    /// <summary>cbc:TaxAmount.</summary>
    [XmlElement("TaxAmount", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public AmountType? TaxAmount { get; set; }

    /// <summary>cac:TaxSubtotal.</summary>
    [XmlElement("TaxSubtotal", Namespace = EspaciosUbl.Cac, Order = 1)]
    public Collection<TaxSubtotal> TaxSubtotal { get; } = [];
}

/// <summary>cac:TaxSubtotal. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TaxSubtotal
{
    /// <summary>cbc:TaxableAmount.</summary>
    [XmlElement("TaxableAmount", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public AmountType? TaxableAmount { get; set; }

    /// <summary>cbc:TaxAmount.</summary>
    [XmlElement("TaxAmount", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public AmountType? TaxAmount { get; set; }

    /// <summary>cbc:BaseUnitMeasure (cantidad de bolsas del ICBPER).</summary>
    [XmlElement("BaseUnitMeasure", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public MeasureType? BaseUnitMeasure { get; set; }

    /// <summary>cac:TaxCategory.</summary>
    [XmlElement("TaxCategory", Namespace = EspaciosUbl.Cac, Order = 3)]
    public TaxCategory? TaxCategory { get; set; }
}

/// <summary>cac:TaxCategory. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TaxCategory
{
    /// <summary>cbc:Percent.</summary>
    [XmlElement("Percent", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public NumericType? Percent { get; set; }

    /// <summary>cbc:PerUnitAmount (tarifa del ICBPER).</summary>
    [XmlElement("PerUnitAmount", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public AmountType? PerUnitAmount { get; set; }

    /// <summary>cbc:TaxExemptionReasonCode (afectación, catálogo N.° 07).</summary>
    [XmlElement("TaxExemptionReasonCode", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public CodeType? TaxExemptionReasonCode { get; set; }

    /// <summary>cbc:TierRange (sistema de ISC, catálogo N.° 08).</summary>
    [XmlElement("TierRange", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public string? TierRange { get; set; }

    /// <summary>cac:TaxScheme.</summary>
    [XmlElement("TaxScheme", Namespace = EspaciosUbl.Cac, Order = 4)]
    public TaxScheme? TaxScheme { get; set; }
}

/// <summary>cac:TaxScheme. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TaxScheme
{
    /// <summary>cbc:ID (catálogo N.° 05).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:Name.</summary>
    [XmlElement("Name", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? Name { get; set; }

    /// <summary>cbc:TaxTypeCode.</summary>
    [XmlElement("TaxTypeCode", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public string? TaxTypeCode { get; set; }
}

/// <summary>cac:LegalMonetaryTotal. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MonetaryTotal
{
    /// <summary>cbc:LineExtensionAmount.</summary>
    [XmlElement("LineExtensionAmount", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public AmountType? LineExtensionAmount { get; set; }

    /// <summary>cbc:TaxInclusiveAmount.</summary>
    [XmlElement("TaxInclusiveAmount", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public AmountType? TaxInclusiveAmount { get; set; }

    /// <summary>cbc:AllowanceTotalAmount.</summary>
    [XmlElement("AllowanceTotalAmount", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public AmountType? AllowanceTotalAmount { get; set; }

    /// <summary>cbc:ChargeTotalAmount.</summary>
    [XmlElement("ChargeTotalAmount", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public AmountType? ChargeTotalAmount { get; set; }

    /// <summary>cbc:PrepaidAmount.</summary>
    [XmlElement("PrepaidAmount", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public AmountType? PrepaidAmount { get; set; }

    /// <summary>cbc:PayableAmount.</summary>
    [XmlElement("PayableAmount", Namespace = EspaciosUbl.Cbc, Order = 5)]
    public AmountType? PayableAmount { get; set; }
}

/// <summary>cac:OrderReference. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OrderReference
{
    /// <summary>cbc:ID.</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc)]
    public IdentifierType? ID { get; set; }
}

/// <summary>cac:DocumentReference (guías, documentos relacionados, anticipos, contrato). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DocumentReference
{
    /// <summary>cbc:ID.</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:DocumentTypeCode.</summary>
    [XmlElement("DocumentTypeCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? DocumentTypeCode { get; set; }

    /// <summary>cbc:DocumentStatusCode (identificador del pago del anticipo).</summary>
    [XmlElement("DocumentStatusCode", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public CodeType? DocumentStatusCode { get; set; }

    /// <summary>cbc:DocumentDescription.</summary>
    [XmlElement("DocumentDescription", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public string? DocumentDescription { get; set; }

    /// <summary>cac:IssuerParty.</summary>
    [XmlElement("IssuerParty", Namespace = EspaciosUbl.Cac, Order = 4)]
    public Party? IssuerParty { get; set; }
}

/// <summary>cac:BillingReference (documento que modifica una nota o una línea del resumen). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BillingReference
{
    /// <summary>cac:InvoiceDocumentReference.</summary>
    [XmlElement("InvoiceDocumentReference", Namespace = EspaciosUbl.Cac)]
    public DocumentReference? InvoiceDocumentReference { get; set; }
}

/// <summary>cac:DiscrepancyResponse (motivo de la nota de crédito o débito). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Response
{
    /// <summary>cbc:ReferenceID (serie-número del documento que modifica).</summary>
    [XmlElement("ReferenceID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public string? ReferenceID { get; set; }

    /// <summary>cbc:ResponseCode (catálogo N.° 09 o N.° 10).</summary>
    [XmlElement("ResponseCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? ResponseCode { get; set; }

    /// <summary>cbc:Description (sustento).</summary>
    [XmlElement("Description", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public Collection<string> Description { get; } = [];
}

/// <summary>cac:PricingReference. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class PricingReference
{
    /// <summary>cac:AlternativeConditionPrice.</summary>
    [XmlElement("AlternativeConditionPrice", Namespace = EspaciosUbl.Cac)]
    public Collection<Price> AlternativeConditionPrice { get; } = [];
}

/// <summary>cac:Price / cac:AlternativeConditionPrice. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Price
{
    /// <summary>cbc:PriceAmount.</summary>
    [XmlElement("PriceAmount", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public AmountType? PriceAmount { get; set; }

    /// <summary>cbc:PriceTypeCode (catálogo N.° 16).</summary>
    [XmlElement("PriceTypeCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? PriceTypeCode { get; set; }
}

/// <summary>cac:Item. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Item
{
    /// <summary>cbc:Description.</summary>
    [XmlElement("Description", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public Collection<string> Description { get; } = [];

    /// <summary>cac:SellersItemIdentification (código interno).</summary>
    [XmlElement("SellersItemIdentification", Namespace = EspaciosUbl.Cac, Order = 1)]
    public ItemIdentification? SellersItemIdentification { get; set; }

    /// <summary>cac:StandardItemIdentification (GTIN).</summary>
    [XmlElement("StandardItemIdentification", Namespace = EspaciosUbl.Cac, Order = 2)]
    public ItemIdentification? StandardItemIdentification { get; set; }

    /// <summary>cac:CommodityClassification (código de producto SUNAT).</summary>
    [XmlElement("CommodityClassification", Namespace = EspaciosUbl.Cac, Order = 3)]
    public Collection<CommodityClassification> CommodityClassification { get; } = [];
}

/// <summary>cac:*ItemIdentification. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ItemIdentification
{
    /// <summary>cbc:ID.</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc)]
    public IdentifierType? ID { get; set; }
}

/// <summary>cac:CommodityClassification. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CommodityClassification
{
    /// <summary>cbc:ItemClassificationCode (UNSPSC).</summary>
    [XmlElement("ItemClassificationCode", Namespace = EspaciosUbl.Cbc)]
    public CodeType? ItemClassificationCode { get; set; }
}
