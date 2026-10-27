using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

// Guía de remisión electrónica (GRE 2.0) en UBL 2.1, perfil SUNAT, con el orden del XSD UBL-DespatchAdvice-2.1: solo
// los elementos que usan el Excel GRE y la XSL GRE 2.0 de la guía remitente.
// Públicas solo por XmlSerializer: NO forman parte del contrato estable de PasheCPE y pueden cambiar sin aviso.
namespace PasheCPE.Ubl;

/// <summary>DespatchAdvice UBL 2.1 (GRE 2.0). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[XmlRoot("DespatchAdvice", Namespace = EspaciosUbl.DespatchAdvice)]
public sealed class DespatchAdvice
{
    /// <summary>ext:UBLExtensions.</summary>
    [XmlElement("UBLExtensions", Namespace = EspaciosUbl.Ext, Order = 0)]
    public UBLExtensions? UBLExtensions { get; set; }

    /// <summary>cbc:UBLVersionID (2.1).</summary>
    [XmlElement("UBLVersionID", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? UBLVersionID { get; set; }

    /// <summary>cbc:CustomizationID (2.0).</summary>
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

    /// <summary>cbc:DespatchAdviceTypeCode (09 o 31).</summary>
    [XmlElement("DespatchAdviceTypeCode", Namespace = EspaciosUbl.Cbc, Order = 6)]
    public CodeType? DespatchAdviceTypeCode { get; set; }

    /// <summary>cbc:Note (observaciones).</summary>
    [XmlElement("Note", Namespace = EspaciosUbl.Cbc, Order = 7)]
    public string? Note { get; set; }

    /// <summary>cac:AdditionalDocumentReference (documentos relacionados, catálogo N.° 61).</summary>
    [XmlElement("AdditionalDocumentReference", Namespace = EspaciosUbl.Cac, Order = 8)]
    public Collection<DocumentReference> AdditionalDocumentReference { get; } = [];

    /// <summary>cac:Signature.</summary>
    [XmlElement("Signature", Namespace = EspaciosUbl.Cac, Order = 9)]
    public Collection<Signature> Signature { get; } = [];

    /// <summary>cac:DespatchSupplierParty (remitente).</summary>
    [XmlElement("DespatchSupplierParty", Namespace = EspaciosUbl.Cac, Order = 10)]
    public AccountParty? DespatchSupplierParty { get; set; }

    /// <summary>cac:DeliveryCustomerParty (destinatario).</summary>
    [XmlElement("DeliveryCustomerParty", Namespace = EspaciosUbl.Cac, Order = 11)]
    public AccountParty? DeliveryCustomerParty { get; set; }

    /// <summary>cac:BuyerCustomerParty (comprador: motivo 03, venta con entrega a terceros).</summary>
    [XmlElement("BuyerCustomerParty", Namespace = EspaciosUbl.Cac, Order = 12)]
    public AccountParty? BuyerCustomerParty { get; set; }

    /// <summary>cac:SellerSupplierParty (proveedor: motivo 02, compra).</summary>
    [XmlElement("SellerSupplierParty", Namespace = EspaciosUbl.Cac, Order = 13)]
    public AccountParty? SellerSupplierParty { get; set; }

    /// <summary>cac:Shipment (datos del traslado).</summary>
    [XmlElement("Shipment", Namespace = EspaciosUbl.Cac, Order = 14)]
    public Shipment? Shipment { get; set; }

    /// <summary>cac:DespatchLine (bienes).</summary>
    [XmlElement("DespatchLine", Namespace = EspaciosUbl.Cac, Order = 15)]
    public Collection<DespatchLine> DespatchLine { get; } = [];
}

/// <summary>cac:Shipment. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Shipment
{
    /// <summary>cbc:ID (SUNAT_Envio).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:HandlingCode (motivo de traslado, catálogo N.° 20).</summary>
    [XmlElement("HandlingCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public CodeType? HandlingCode { get; set; }

    /// <summary>cbc:HandlingInstructions (descripción del motivo).</summary>
    [XmlElement("HandlingInstructions", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public string? HandlingInstructions { get; set; }

    /// <summary>cbc:GrossWeightMeasure (peso bruto total).</summary>
    [XmlElement("GrossWeightMeasure", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public MeasureType? GrossWeightMeasure { get; set; }

    /// <summary>cbc:TotalTransportHandlingUnitQuantity (número de bultos o pallets).</summary>
    [XmlElement("TotalTransportHandlingUnitQuantity", Namespace = EspaciosUbl.Cbc, Order = 4)]
    public string? TotalTransportHandlingUnitQuantity { get; set; }

    /// <summary>cbc:SpecialInstructions (indicadores SUNAT_Envio_Indicador…).</summary>
    [XmlElement("SpecialInstructions", Namespace = EspaciosUbl.Cbc, Order = 5)]
    public Collection<string> SpecialInstructions { get; } = [];

    /// <summary>cac:ShipmentStage (modalidad, fechas, transportista y conductores).</summary>
    [XmlElement("ShipmentStage", Namespace = EspaciosUbl.Cac, Order = 6)]
    public Collection<ShipmentStage> ShipmentStage { get; } = [];

    /// <summary>cac:Delivery (puntos de llegada y de partida).</summary>
    [XmlElement("Delivery", Namespace = EspaciosUbl.Cac, Order = 7)]
    public Delivery? Delivery { get; set; }

    /// <summary>cac:TransportHandlingUnit (vehículos).</summary>
    [XmlElement("TransportHandlingUnit", Namespace = EspaciosUbl.Cac, Order = 8)]
    public Collection<TransportHandlingUnit> TransportHandlingUnit { get; } = [];

    /// <summary>cac:FirstArrivalPortLocation (puerto o aeropuerto).</summary>
    [XmlElement("FirstArrivalPortLocation", Namespace = EspaciosUbl.Cac, Order = 9)]
    public Location? FirstArrivalPortLocation { get; set; }
}

/// <summary>cac:ShipmentStage. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ShipmentStage
{
    /// <summary>cbc:TransportModeCode (modalidad, catálogo N.° 18).</summary>
    [XmlElement("TransportModeCode", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public CodeType? TransportModeCode { get; set; }

    /// <summary>cac:TransitPeriod (fecha de inicio del traslado).</summary>
    [XmlElement("TransitPeriod", Namespace = EspaciosUbl.Cac, Order = 1)]
    public Period? TransitPeriod { get; set; }

    /// <summary>cac:CarrierParty (transportista).</summary>
    [XmlElement("CarrierParty", Namespace = EspaciosUbl.Cac, Order = 2)]
    public Collection<Party> CarrierParty { get; } = [];

    /// <summary>cac:LoadingTransportEvent (fecha de entrega de los bienes al transportista).</summary>
    [XmlElement("LoadingTransportEvent", Namespace = EspaciosUbl.Cac, Order = 3)]
    public TransportEvent? LoadingTransportEvent { get; set; }

    /// <summary>cac:DriverPerson (conductores: principal y secundarios).</summary>
    [XmlElement("DriverPerson", Namespace = EspaciosUbl.Cac, Order = 4)]
    public Collection<Person> DriverPerson { get; } = [];
}

/// <summary>cac:TransitPeriod. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Period
{
    /// <summary>cbc:StartDate (yyyy-MM-dd).</summary>
    [XmlElement("StartDate", Namespace = EspaciosUbl.Cbc)]
    public string? StartDate { get; set; }
}

/// <summary>cac:LoadingTransportEvent. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TransportEvent
{
    /// <summary>cbc:OccurrenceDate (yyyy-MM-dd).</summary>
    [XmlElement("OccurrenceDate", Namespace = EspaciosUbl.Cbc)]
    public string? OccurrenceDate { get; set; }
}

/// <summary>cac:DriverPerson. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Person
{
    /// <summary>cbc:ID (documento de identidad del conductor).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:FirstName (nombres).</summary>
    [XmlElement("FirstName", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? FirstName { get; set; }

    /// <summary>cbc:FamilyName (apellidos).</summary>
    [XmlElement("FamilyName", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public string? FamilyName { get; set; }

    /// <summary>cbc:JobTitle (Principal o Secundario).</summary>
    [XmlElement("JobTitle", Namespace = EspaciosUbl.Cbc, Order = 3)]
    public string? JobTitle { get; set; }

    /// <summary>cac:IdentityDocumentReference (licencia de conducir).</summary>
    [XmlElement("IdentityDocumentReference", Namespace = EspaciosUbl.Cac, Order = 4)]
    public DocumentReference? IdentityDocumentReference { get; set; }
}

/// <summary>cac:Delivery. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Delivery
{
    /// <summary>cac:DeliveryAddress (punto de llegada).</summary>
    [XmlElement("DeliveryAddress", Namespace = EspaciosUbl.Cac, Order = 0)]
    public Address? DeliveryAddress { get; set; }

    /// <summary>cac:Despatch (punto de partida).</summary>
    [XmlElement("Despatch", Namespace = EspaciosUbl.Cac, Order = 1)]
    public Despatch? Despatch { get; set; }
}

/// <summary>cac:Despatch. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Despatch
{
    /// <summary>cac:DespatchAddress (punto de partida).</summary>
    [XmlElement("DespatchAddress", Namespace = EspaciosUbl.Cac)]
    public Address? DespatchAddress { get; set; }
}

/// <summary>cac:TransportHandlingUnit. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TransportHandlingUnit
{
    /// <summary>cac:TransportEquipment (vehículo principal y sus secundarios).</summary>
    [XmlElement("TransportEquipment", Namespace = EspaciosUbl.Cac)]
    public TransportEquipment? TransportEquipment { get; set; }
}

/// <summary>cac:TransportEquipment / cac:AttachedTransportEquipment. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TransportEquipment
{
    /// <summary>cbc:ID (placa).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cac:ApplicableTransportMeans (Tarjeta Única de Circulación Electrónica o certificado de habilitación).</summary>
    [XmlElement("ApplicableTransportMeans", Namespace = EspaciosUbl.Cac, Order = 1)]
    public TransportMeans? ApplicableTransportMeans { get; set; }

    /// <summary>cac:AttachedTransportEquipment (vehículos secundarios).</summary>
    [XmlElement("AttachedTransportEquipment", Namespace = EspaciosUbl.Cac, Order = 2)]
    public Collection<TransportEquipment> AttachedTransportEquipment { get; } = [];

    /// <summary>cac:ShipmentDocumentReference (autorización especial del vehículo, entidad en schemeID).</summary>
    [XmlElement("ShipmentDocumentReference", Namespace = EspaciosUbl.Cac, Order = 3)]
    public DocumentReference? ShipmentDocumentReference { get; set; }
}

/// <summary>cac:ApplicableTransportMeans. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TransportMeans
{
    /// <summary>cbc:RegistrationNationalityID (TUCE o certificado de habilitación vehicular).</summary>
    [XmlElement("RegistrationNationalityID", Namespace = EspaciosUbl.Cbc)]
    public string? RegistrationNationalityID { get; set; }
}

/// <summary>cac:FirstArrivalPortLocation. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Location
{
    /// <summary>cbc:ID (código del puerto, aeropuerto o centro de atención en frontera).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:LocationTypeCode (1 puerto, 2 aeropuerto).</summary>
    [XmlElement("LocationTypeCode", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public string? LocationTypeCode { get; set; }

    /// <summary>cbc:Name.</summary>
    [XmlElement("Name", Namespace = EspaciosUbl.Cbc, Order = 2)]
    public string? Name { get; set; }
}

/// <summary>cac:DespatchLine. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DespatchLine
{
    /// <summary>cbc:ID (número de orden del ítem).</summary>
    [XmlElement("ID", Namespace = EspaciosUbl.Cbc, Order = 0)]
    public IdentifierType? ID { get; set; }

    /// <summary>cbc:DeliveredQuantity (cantidad con su unidad).</summary>
    [XmlElement("DeliveredQuantity", Namespace = EspaciosUbl.Cbc, Order = 1)]
    public QuantityType? DeliveredQuantity { get; set; }

    /// <summary>cac:OrderLineReference (obligatorio en el XSD UBL: el mismo número de orden).</summary>
    [XmlElement("OrderLineReference", Namespace = EspaciosUbl.Cac, Order = 2)]
    public Collection<LineReference> OrderLineReference { get; } = [];

    /// <summary>cac:Item (descripción, códigos y propiedades del bien).</summary>
    [XmlElement("Item", Namespace = EspaciosUbl.Cac, Order = 3)]
    public Item? Item { get; set; }
}

/// <summary>cac:OrderLineReference. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LineReference
{
    /// <summary>cbc:LineID.</summary>
    [XmlElement("LineID", Namespace = EspaciosUbl.Cbc)]
    public string? LineID { get; set; }
}
