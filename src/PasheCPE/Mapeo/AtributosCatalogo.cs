using PasheCPE.Ubl;

namespace PasheCPE.Mapeo;

/// <summary>
/// Atributos de catálogo de los elementos UBL según la guía de elaboración de SUNAT (listAgencyName, listName,
/// listURI, schemeName…). Son metadatos de formato del XML, no datos tributarios; viven aquí en un solo lugar.
/// </summary>
internal static class AtributosCatalogo
{
    private const string _sunat = "PE:SUNAT";
    private const string _uneceAgencia = "United Nations Economic Commission for Europe";
    private const string _uriCatalogo = "urn:pe:gob:sunat:cpe:see:gem:catalogos:catalogo";

    public static IdentifierType Personalizacion(string valor) => new() { SchemeAgencyName = _sunat, Value = valor };

    public static CodeType TipoDocumento(string tipo, string tipoOperacion) => new()
    {
        ListId = tipoOperacion,
        ListAgencyName = _sunat,
        ListName = "Tipo de Documento",
        ListUri = _uriCatalogo + "01",
        Name = "Tipo de Operacion",
        ListSchemeUri = _uriCatalogo + "51",
        Value = tipo,
    };

    public static CodeType Moneda(string moneda) => new()
    {
        ListId = "ISO 4217 Alpha",
        ListName = "Currency",
        ListAgencyName = _uneceAgencia,
        Value = moneda,
    };

    public static IdentifierType DocumentoIdentidad(string tipo, string numero) => new()
    {
        SchemeId = tipo,
        SchemeName = "Documento de Identidad",
        SchemeAgencyName = _sunat,
        SchemeUri = _uriCatalogo + "06",
        Value = numero,
    };

    public static IdentifierType Ubigeo(string ubigeo) => new() { SchemeName = "Ubigeos", SchemeAgencyName = "PE:INEI", Value = ubigeo };

    public static CodeType Establecimiento(string codigo) => new() { ListAgencyName = _sunat, ListName = "Establecimientos anexos", Value = codigo };

    public static CodeType Pais(string pais) => new() { ListId = "ISO 3166-1", ListAgencyName = _uneceAgencia, ListName = "Country", Value = pais };

    public static QuantityType Cantidad(decimal cantidad, string unidad) => new()
    {
        UnitCode = unidad,
        UnitCodeListId = "UN/ECE rec 20",
        UnitCodeListAgencyName = _uneceAgencia,
        Valor = cantidad,
    };

    public static CodeType TipoPrecio(string tipo) => new() { ListName = "Tipo de Precio", ListAgencyName = _sunat, ListUri = _uriCatalogo + "16", Value = tipo };

    public static CodeType Afectacion(string afectacion) => new()
    {
        ListAgencyName = _sunat,
        ListName = "Afectacion del IGV",
        ListUri = _uriCatalogo + "07",
        Value = afectacion,
    };

    public static IdentifierType Tributo(string tributo) => new()
    {
        SchemeName = "Codigo de tributos",
        SchemeAgencyName = _sunat,
        SchemeUri = _uriCatalogo + "05",
        Value = tributo,
    };

    public static CodeType CargoDescuento(string codigo) => new()
    {
        ListAgencyName = _sunat,
        ListName = "Cargo/descuento",
        ListUri = _uriCatalogo + "53",
        Value = codigo,
    };

    public static CodeType ProductoSunat(string codigo) => new() { ListId = "UNSPSC", ListAgencyName = "GS1 US", ListName = "Item Classification", Value = codigo };

    public static IdentifierType Gtin(string gtin) => new() { SchemeId = "GTIN-" + gtin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), Value = gtin };

    public static CodeType DocumentoRelacionado(string tipo) => new()
    {
        ListAgencyName = _sunat,
        ListName = "Documento Relacionado",
        ListUri = _uriCatalogo + "12",
        Value = tipo,
    };

    public static CodeType Guia(string tipo) => new()
    {
        ListAgencyName = _sunat,
        ListName = "Tipo de Documento",
        ListUri = _uriCatalogo + "01",
        Value = tipo,
    };

    public static CodeType IdentificadorAnticipo(string identificador) => new() { ListName = "Anticipo", ListAgencyName = _sunat, Value = identificador };

    public static IdentifierType PagoAnticipo(string identificador) => new() { SchemeName = "Anticipo", SchemeAgencyName = _sunat, Value = identificador };

    public static CodeType MedioPago(string codigo) => new() { ListName = "Medio de pago", ListAgencyName = _sunat, ListUri = _uriCatalogo + "59", Value = codigo };

    public static IdentifierType BienDetraccion(string codigo) => new()
    {
        SchemeName = "SUNAT:Codigo de detraccion",
        SchemeAgencyName = _sunat,
        SchemeUri = _uriCatalogo + "54",
        Value = codigo,
    };
}
