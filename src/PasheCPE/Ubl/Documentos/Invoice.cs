using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

// Factura (01) y boleta (03) en UBL 2.1, perfil SUNAT. Públicas solo por XmlSerializer: NO forman parte del contrato
// estable de PasheCPE y pueden cambiar en versiones menores sin aviso.
namespace PasheCPE.Ubl;

/// <summary>Invoice UBL 2.1 (perfil SUNAT). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[XmlRoot("Invoice", Namespace = EspaciosUbl.Invoice)]
public sealed class Invoice
{
    /// <summary>ext:UBLExtensions.</summary>
    [XmlElement("UBLExtensions", Namespace = EspaciosUbl.Ext, Order = 0)]
    public UBLExtensions? UBLExtensions { get; set; }

    /// <summary>cbc:UBLVersionID.</summary>
    [XmlElement("UBLVersionID", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? UBLVersionID { get; set; }

    /// <summary>cbc:CustomizationID.</summary>
    [XmlElement("CustomizationID", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public IdentifierType? CustomizationID { get; set; }

    /// <summary>cbc:ID (serie-número).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:IssueDate (yyyy-MM-dd).</summary>
    [XmlElement("IssueDate", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public string? IssueDate { get; set; }

    /// <summary>cbc:IssueTime (HH:mm:ss).</summary>
    [XmlElement("IssueTime", Namespace = EspaciosUbl.Cbc, Order = 5)]
    public string? IssueTime { get; set; }

    /// <summary>cbc:DueDate (yyyy-MM-dd).</summary>
    [XmlElement("DueDate", Namespace = EspaciosUbl.Cbc, Order = 6)]
    public string? DueDate { get; set; }

    /// <summary>cbc:InvoiceTypeCode (listID = tipo de operación).</summary>
    [XmlElement("InvoiceTypeCode", Namespace = EspaciosUbl.Cbc, Order = 7)]
    public CodeType? InvoiceTypeCode { get; set; }

    /// <summary>cbc:Note (leyendas).</summary>
    [XmlElement("Note", Namespace = EspaciosUbl.Cbc, Order = 8)]
    public Collection<TextType> Note { get; } = [];

    /// <summary>cbc:DocumentCurrencyCode.</summary>
    [XmlElement("DocumentCurrencyCode", Namespace = EspaciosUbl.Cbc, Order = 9)]
    public CodeType? DocumentCurrencyCode { get; set; }

    /// <summary>cbc:LineCountNumeric.</summary>
    [XmlElement("LineCountNumeric", Namespace = EspaciosUbl.Cbc, Order = 10)]
    public NumericType? LineCountNumeric { get; set; }

    /// <summary>cac:OrderReference.</summary>
    [XmlElement("OrderReference", Namespace = EspaciosUbl.Cac, Order = 11)]
    public OrderReference? OrderReference { get; set; }

    /// <summary>cac:DespatchDocumentReference (guías de remisión).</summary>
    [XmlElement("DespatchDocumentReference", Namespace = EspaciosUbl.Cac, Order = 12)]
    public Collection<DocumentReference> DespatchDocumentReference { get; } = [];

    /// <summary>cac:ContractDocumentReference (contrato de colaboración empresarial).</summary>
    [XmlElement("ContractDocumentReference", Namespace = EspaciosUbl.Cac, Order = 13)]
    public Collection<DocumentReference> ContractDocumentReference { get; } = [];

    /// <summary>cac:AdditionalDocumentReference (documentos relacionados y anticipos).</summary>
    [XmlElement("AdditionalDocumentReference", Namespace = EspaciosUbl.Cac, Order = 14)]
    public Collection<DocumentReference> AdditionalDocumentReference { get; } = [];

    /// <summary>cac:Signature.</summary>
    [XmlElement("Signature", Namespace = EspaciosUbl.Cac, Order = 15)]
    public Collection<Signature> Signature { get; } = [];

    /// <summary>cac:AccountingSupplierParty.</summary>
    [XmlElement("AccountingSupplierParty", Namespace = EspaciosUbl.Cac, Order = 16)]
    public PartyRole? AccountingSupplierParty { get; set; }

    /// <summary>cac:AccountingCustomerParty.</summary>
    [XmlElement("AccountingCustomerParty", Namespace = EspaciosUbl.Cac, Order = 17)]
    public PartyRole? AccountingCustomerParty { get; set; }

    /// <summary>cac:PaymentMeans.</summary>
    [XmlElement("PaymentMeans", Namespace = EspaciosUbl.Cac, Order = 18)]
    public Collection<PaymentMeans> PaymentMeans { get; } = [];

    /// <summary>cac:PaymentTerms.</summary>
    [XmlElement("PaymentTerms", Namespace = EspaciosUbl.Cac, Order = 19)]
    public Collection<PaymentTerms> PaymentTerms { get; } = [];

    /// <summary>cac:PrepaidPayment.</summary>
    [XmlElement("PrepaidPayment", Namespace = EspaciosUbl.Cac, Order = 20)]
    public Collection<Payment> PrepaidPayment { get; } = [];

    /// <summary>cac:AllowanceCharge.</summary>
    [XmlElement("AllowanceCharge", Namespace = EspaciosUbl.Cac, Order = 21)]
    public Collection<AllowanceCharge> AllowanceCharge { get; } = [];

    /// <summary>cac:TaxTotal.</summary>
    [XmlElement("TaxTotal", Namespace = EspaciosUbl.Cac, Order = 22)]
    public Collection<TaxTotal> TaxTotal { get; } = [];

    /// <summary>cac:LegalMonetaryTotal.</summary>
    [XmlElement("LegalMonetaryTotal", Namespace = EspaciosUbl.Cac, Order = 23)]
    public MonetaryTotal? LegalMonetaryTotal { get; set; }

    /// <summary>cac:InvoiceLine.</summary>
    [XmlElement("InvoiceLine", Namespace = EspaciosUbl.Cac, Order = 24)]
    public Collection<InvoiceLine> InvoiceLine { get; } = [];
}

/// <summary>cac:InvoiceLine. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class InvoiceLine
{
    /// <summary>cbc:ID (número de línea).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:InvoicedQuantity.</summary>
    [XmlElement("InvoicedQuantity", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public QuantityType? InvoicedQuantity { get; set; }

    /// <summary>cbc:LineExtensionAmount (valor de venta de la línea).</summary>
    [XmlElement("LineExtensionAmount", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public AmountType? LineExtensionAmount { get; set; }

    /// <summary>cac:PricingReference.</summary>
    [XmlElement("PricingReference", Namespace = EspaciosUbl.Cac, Order = 3)]
    public PricingReference? PricingReference { get; set; }

    /// <summary>cac:AllowanceCharge (descuento de línea).</summary>
    [XmlElement("AllowanceCharge", Namespace = EspaciosUbl.Cac, Order = 4)]
    public Collection<AllowanceCharge> AllowanceCharge { get; } = [];

    /// <summary>cac:TaxTotal.</summary>
    [XmlElement("TaxTotal", Namespace = EspaciosUbl.Cac, Order = 5)]
    public Collection<TaxTotal> TaxTotal { get; } = [];

    /// <summary>cac:Item.</summary>
    [XmlElement("Item", Namespace = EspaciosUbl.Cac, Order = 6)]
    public Item? Item { get; set; }

    /// <summary>cac:Price (valor unitario).</summary>
    [XmlElement("Price", Namespace = EspaciosUbl.Cac, Order = 7)]
    public Price? Price { get; set; }
}
