using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

// Resumen diario de boletas y notas (RC) en UBL 2.0, perfil SUNAT 1.1, con el orden del XSD UBLPE-SummaryDocuments-1.0.
// Públicas solo por XmlSerializer: NO forman parte del contrato estable de PasheCPE y pueden cambiar sin aviso.
namespace PasheCPE.Ubl;

/// <summary>SummaryDocuments UBL 2.0 (perfil SUNAT 1.1). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[XmlRoot("SummaryDocuments", Namespace = EspaciosUbl.SummaryDocuments)]
public sealed class SummaryDocuments
{
    /// <summary>ext:UBLExtensions.</summary>
    [XmlElement("UBLExtensions", Namespace = EspaciosUbl.Ext, Order = 0)]
    public UBLExtensions? UBLExtensions { get; set; }

    /// <summary>cbc:UBLVersionID (2.0).</summary>
    [XmlElement("UBLVersionID", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? UBLVersionID { get; set; }

    /// <summary>cbc:CustomizationID (1.1).</summary>
    [XmlElement("CustomizationID", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public IdentifierType? CustomizationID { get; set; }

    /// <summary>cbc:ID (RC-yyyyMMdd-n).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:ReferenceDate: fecha de emisión de los documentos (yyyy-MM-dd).</summary>
    [XmlElement("ReferenceDate", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public string? ReferenceDate { get; set; }

    /// <summary>cbc:IssueDate: fecha de generación del resumen (yyyy-MM-dd).</summary>
    [XmlElement("IssueDate", Namespace = EspaciosUbl.Cbc, Order = 5)]
    public string? IssueDate { get; set; }

    /// <summary>cac:Signature.</summary>
    [XmlElement("Signature", Namespace = EspaciosUbl.Cac, Order = 6)]
    public Collection<Signature> Signature { get; } = [];

    /// <summary>cac:AccountingSupplierParty.</summary>
    [XmlElement("AccountingSupplierParty", Namespace = EspaciosUbl.Cac, Order = 7)]
    public AccountParty? AccountingSupplierParty { get; set; }

    /// <summary>sac:SummaryDocumentsLine.</summary>
    [XmlElement("SummaryDocumentsLine", Namespace = EspaciosUbl.Sac, Order = 8)]
    public Collection<SummaryDocumentsLine> SummaryDocumentsLine { get; } = [];
}

/// <summary>
/// Emisor o adquiriente en UBL 2.0 (SupplierParty / CustomerParty): documento en <c>CustomerAssignedAccountID</c>, tipo
/// en <c>AdditionalAccountID</c> y razón social. No es parte del contrato estable.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AccountParty
{
    /// <summary>cbc:CustomerAssignedAccountID (número de documento).</summary>
    [XmlElement("CustomerAssignedAccountID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public string? CustomerAssignedAccountID { get; set; }

    /// <summary>cbc:AdditionalAccountID (tipo de documento, catálogo N.° 06).</summary>
    [XmlElement("AdditionalAccountID", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? AdditionalAccountID { get; set; }

    /// <summary>cac:Party (razón social en PartyLegalEntity).</summary>
    [XmlElement("Party", Namespace = EspaciosUbl.Cac, Order = 2)]
    public Party? Party { get; set; }
}

/// <summary>sac:SummaryDocumentsLine. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SummaryDocumentsLine
{
    /// <summary>cbc:LineID.</summary>
    [XmlElement("LineID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public string? LineID { get; set; }

    /// <summary>cbc:DocumentTypeCode (03, 07 u 08).</summary>
    [XmlElement("DocumentTypeCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? DocumentTypeCode { get; set; }

    /// <summary>cbc:ID (serie-número).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public IdentifierType? ID { get; set; }

    /// <summary>cac:AccountingCustomerParty.</summary>
    [XmlElement("AccountingCustomerParty", Namespace = EspaciosUbl.Cac, Order = 3)]
    public AccountParty? AccountingCustomerParty { get; set; }

    /// <summary>cac:BillingReference (boleta que modifica la nota).</summary>
    [XmlElement("BillingReference", Namespace = EspaciosUbl.Cac, Order = 4)]
    public BillingReference? BillingReference { get; set; }

    /// <summary>cac:Status/cbc:ConditionCode (catálogo N.° 19).</summary>
    [XmlElement("Status", Namespace = EspaciosUbl.Cac, Order = 5)]
    public LineStatus? Status { get; set; }

    /// <summary>sac:TotalAmount (importe total).</summary>
    [XmlElement("TotalAmount", Namespace = EspaciosUbl.Sac, Order = 6)]
    public AmountType? TotalAmount { get; set; }

    /// <summary>sac:BillingPayment (total por tipo de valor de venta, catálogo N.° 11).</summary>
    [XmlElement("BillingPayment", Namespace = EspaciosUbl.Sac, Order = 7)]
    public Collection<BillingPayment> BillingPayment { get; } = [];

    /// <summary>cac:AllowanceCharge (otros cargos).</summary>
    [XmlElement("AllowanceCharge", Namespace = EspaciosUbl.Cac, Order = 8)]
    public Collection<AllowanceCharge> AllowanceCharge { get; } = [];

    /// <summary>cac:TaxTotal (un tributo por TaxTotal).</summary>
    [XmlElement("TaxTotal", Namespace = EspaciosUbl.Cac, Order = 9)]
    public Collection<TaxTotal> TaxTotal { get; } = [];
}

/// <summary>cac:BillingReference. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BillingReference
{
    /// <summary>cac:InvoiceDocumentReference.</summary>
    [XmlElement("InvoiceDocumentReference", Namespace = EspaciosUbl.Cac)]
    public DocumentReference? InvoiceDocumentReference { get; set; }
}

/// <summary>cac:Status. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LineStatus
{
    /// <summary>cbc:ConditionCode.</summary>
    [XmlElement("ConditionCode", Namespace = EspaciosUbl.Cbc)]
    public string? ConditionCode { get; set; }
}

/// <summary>sac:BillingPayment. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BillingPayment
{
    /// <summary>cbc:PaidAmount.</summary>
    [XmlElement("PaidAmount", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public AmountType? PaidAmount { get; set; }

    /// <summary>cbc:InstructionID (catálogo N.° 11).</summary>
    [XmlElement("InstructionID", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? InstructionID { get; set; }
}
