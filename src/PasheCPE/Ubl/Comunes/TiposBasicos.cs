using System.ComponentModel;
using System.Globalization;
using System.Xml.Serialization;

// Perfil UBL 2.1 de SUNAT (TRD §3.1). Las clases son públicas solo porque XmlSerializer y su pregeneración lo exigen:
// NO forman parte del contrato estable de PasheCPE y pueden cambiar en versiones menores sin aviso.
namespace PasheCPE.Ubl;

/// <summary>Namespaces UBL usados por el perfil SUNAT.</summary>
internal static class EspaciosUbl
{
    public const string Invoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public const string Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    public const string Ext = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";
    public const string Ds = "http://www.w3.org/2000/09/xmldsig#";
}

/// <summary>Formato de los valores numéricos del XML: siempre <see cref="CultureInfo.InvariantCulture"/>.</summary>
internal static class FormatoUbl
{
    /// <summary>Montos: 2 decimales fijos.</summary>
    public const string Monto = "0.00";

    /// <summary>Precios y valores unitarios: de 2 a 10 decimales.</summary>
    public const string Unitario = "0.00########";

    /// <summary>Cantidades y numéricos: hasta 10 decimales, sin ceros sobrantes.</summary>
    public const string Cantidad = "0.##########";

    public static string Texto(decimal valor, string formato) => valor.ToString(formato, CultureInfo.InvariantCulture);

    public static decimal Leer(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? 0m : decimal.Parse(texto, NumberStyles.Float, CultureInfo.InvariantCulture);
}

/// <summary>UBL AmountType: monto con moneda (cbc:*Amount). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class AmountType
{
    /// <summary>Moneda ISO 4217.</summary>
    [XmlAttribute("currencyID")]
    public string? CurrencyId { get; set; }

    private string? _leido;
    private decimal _valor;

    /// <summary>Valor.</summary>
    [XmlIgnore]
    public decimal Valor
    {
        get => _valor;
        set
        {
            _valor = value;
            _leido = null;
        }
    }

    /// <summary>Formato numérico del texto.</summary>
    [XmlIgnore]
    public string Formato { get; set; } = FormatoUbl.Monto;

    /// <summary>Texto del elemento.</summary>
    [XmlText]
    public string Texto
    {
        get => _leido ?? FormatoUbl.Texto(_valor, Formato);
        set
        {
            _valor = FormatoUbl.Leer(value);
            _leido = value;
        }
    }
}

/// <summary>UBL QuantityType: cantidad con unidad (cbc:InvoicedQuantity). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class QuantityType
{
    /// <summary>Unidad UN/ECE Rec. 20.</summary>
    [XmlAttribute("unitCode")]
    public string? UnitCode { get; set; }

    /// <summary>Lista de la unidad.</summary>
    [XmlAttribute("unitCodeListID")]
    public string? UnitCodeListId { get; set; }

    /// <summary>Agencia de la lista de la unidad.</summary>
    [XmlAttribute("unitCodeListAgencyName")]
    public string? UnitCodeListAgencyName { get; set; }

    private string? _leido;
    private decimal _valor;

    /// <summary>Valor.</summary>
    [XmlIgnore]
    public decimal Valor
    {
        get => _valor;
        set
        {
            _valor = value;
            _leido = null;
        }
    }

    /// <summary>Texto del elemento.</summary>
    [XmlText]
    public string Texto
    {
        get => _leido ?? FormatoUbl.Texto(_valor, FormatoUbl.Cantidad);
        set
        {
            _valor = FormatoUbl.Leer(value);
            _leido = value;
        }
    }
}

/// <summary>UBL MeasureType: medida con unidad (cbc:BaseUnitMeasure). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class MeasureType
{
    /// <summary>Unidad.</summary>
    [XmlAttribute("unitCode")]
    public string? UnitCode { get; set; }

    private string? _leido;
    private decimal _valor;

    /// <summary>Valor.</summary>
    [XmlIgnore]
    public decimal Valor
    {
        get => _valor;
        set
        {
            _valor = value;
            _leido = null;
        }
    }

    /// <summary>Texto del elemento.</summary>
    [XmlText]
    public string Texto
    {
        get => _leido ?? FormatoUbl.Texto(_valor, FormatoUbl.Cantidad);
        set
        {
            _valor = FormatoUbl.Leer(value);
            _leido = value;
        }
    }
}

/// <summary>UBL NumericType (cbc:Percent, cbc:LineCountNumeric…). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NumericType
{
    private string? _leido;
    private decimal _valor;

    /// <summary>Valor.</summary>
    [XmlIgnore]
    public decimal Valor
    {
        get => _valor;
        set
        {
            _valor = value;
            _leido = null;
        }
    }

    /// <summary>Formato numérico del texto.</summary>
    [XmlIgnore]
    public string Formato { get; set; } = FormatoUbl.Cantidad;

    /// <summary>Texto del elemento.</summary>
    [XmlText]
    public string Texto
    {
        get => _leido ?? FormatoUbl.Texto(_valor, Formato);
        set
        {
            _valor = FormatoUbl.Leer(value);
            _leido = value;
        }
    }
}

/// <summary>UBL CodeType con los atributos de catálogo de SUNAT. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class CodeType
{
    /// <summary>listID.</summary>
    [XmlAttribute("listID")]
    public string? ListId { get; set; }

    /// <summary>listAgencyName.</summary>
    [XmlAttribute("listAgencyName")]
    public string? ListAgencyName { get; set; }

    /// <summary>listName.</summary>
    [XmlAttribute("listName")]
    public string? ListName { get; set; }

    /// <summary>listURI.</summary>
    [XmlAttribute("listURI")]
    public string? ListUri { get; set; }

    /// <summary>name.</summary>
    [XmlAttribute("name")]
    public string? Name { get; set; }

    /// <summary>listSchemeURI.</summary>
    [XmlAttribute("listSchemeURI")]
    public string? ListSchemeUri { get; set; }

    /// <summary>Código.</summary>
    [XmlText]
    public string? Value { get; set; }
}

/// <summary>UBL IdentifierType con los atributos de esquema de SUNAT. No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class IdentifierType
{
    /// <summary>schemeID.</summary>
    [XmlAttribute("schemeID")]
    public string? SchemeId { get; set; }

    /// <summary>schemeName.</summary>
    [XmlAttribute("schemeName")]
    public string? SchemeName { get; set; }

    /// <summary>schemeAgencyName.</summary>
    [XmlAttribute("schemeAgencyName")]
    public string? SchemeAgencyName { get; set; }

    /// <summary>schemeURI.</summary>
    [XmlAttribute("schemeURI")]
    public string? SchemeUri { get; set; }

    /// <summary>Identificador.</summary>
    [XmlText]
    public string? Value { get; set; }
}

/// <summary>UBL TextType (cbc:Note con el código de leyenda). No es parte del contrato estable.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TextType
{
    /// <summary>Código de la leyenda (catálogo N.° 52).</summary>
    [XmlAttribute("languageLocaleID")]
    public string? LanguageLocaleId { get; set; }

    /// <summary>Texto.</summary>
    [XmlText]
    public string? Value { get; set; }
}
