using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

// Comunicación de baja (RA) en UBL 2.0, perfil SUNAT 1.0, con el orden del XSD UBLPE-VoidedDocuments-1.0.
// Públicas solo por XmlSerializer: NO forman parte del contrato estable de PasheCPE y pueden cambiar sin aviso.
namespace PasheCPE.Ubl;

/// <summary>VoidedDocuments UBL 2.0 (perfil SUNAT 1.0). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[XmlRoot("VoidedDocuments", Namespace = EspaciosUbl.VoidedDocuments)]
public sealed class VoidedDocuments
{
    /// <summary>ext:UBLExtensions.</summary>
    [XmlElement("UBLExtensions", Namespace = EspaciosUbl.Ext, Order = 0)]
    public UBLExtensions? UBLExtensions { get; set; }

    /// <summary>cbc:UBLVersionID (2.0).</summary>
    [XmlElement("UBLVersionID", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? UBLVersionID { get; set; }

    /// <summary>cbc:CustomizationID (1.0).</summary>
    [XmlElement("CustomizationID", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public IdentifierType? CustomizationID { get; set; }

    /// <summary>cbc:ID (RA-yyyyMMdd-n).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:ReferenceDate: fecha de emisión de los documentos dados de baja (yyyy-MM-dd).</summary>
    [XmlElement("ReferenceDate", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public string? ReferenceDate { get; set; }

    /// <summary>cbc:IssueDate: fecha de generación de la comunicación (yyyy-MM-dd).</summary>
    [XmlElement("IssueDate", Namespace = EspaciosUbl.Cbc, Order = 5)]
    public string? IssueDate { get; set; }

    /// <summary>cac:Signature.</summary>
    [XmlElement("Signature", Namespace = EspaciosUbl.Cac, Order = 6)]
    public Collection<Signature> Signature { get; } = [];

    /// <summary>cac:AccountingSupplierParty.</summary>
    [XmlElement("AccountingSupplierParty", Namespace = EspaciosUbl.Cac, Order = 7)]
    public AccountParty? AccountingSupplierParty { get; set; }

    /// <summary>sac:VoidedDocumentsLine.</summary>
    [XmlElement("VoidedDocumentsLine", Namespace = EspaciosUbl.Sac, Order = 8)]
    public Collection<VoidedDocumentsLine> VoidedDocumentsLine { get; } = [];
}

/// <summary>sac:VoidedDocumentsLine. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class VoidedDocumentsLine
{
    /// <summary>cbc:LineID.</summary>
    [XmlElement("LineID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public string? LineID { get; set; }

    /// <summary>cbc:DocumentTypeCode (01, 07 u 08).</summary>
    [XmlElement("DocumentTypeCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? DocumentTypeCode { get; set; }

    /// <summary>sac:DocumentSerialID (serie).</summary>
    [XmlElement("DocumentSerialID", Namespace = EspaciosUbl.Sac, Order = 2)]
    public string? DocumentSerialID { get; set; }

    /// <summary>sac:DocumentNumberID (número sin ceros a la izquierda).</summary>
    [XmlElement("DocumentNumberID", Namespace = EspaciosUbl.Sac, Order = 3)]
    public string? DocumentNumberID { get; set; }

    /// <summary>sac:VoidReasonDescription (motivo de la baja).</summary>
    [XmlElement("VoidReasonDescription", Namespace = EspaciosUbl.Sac, Order = 4)]
    public string? VoidReasonDescription { get; set; }
}
