using System.Globalization;
using System.Xml.Linq;

namespace PasheCPE.Tests.Dorados;

/// <summary>
/// Lee (solo lectura) una factura aceptada por SUNAT de <c>tests/Datos/Dorados</c> y arma el emisor y el
/// <see cref="ComprobanteCpe"/> equivalentes, como los entregaría el sistema de ventas. Los archivos de Dorados nunca
/// se modifican.
/// </summary>
internal static class LectorDorado
{
    private static readonly XNamespace _cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace _cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public static string Carpeta(string documento)
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, "PasheCPE.slnx")))
            {
                return Path.Combine(carpeta.FullName, "tests", "Datos", "Dorados", documento);
            }
        }

        throw new DirectoryNotFoundException("No se encontró la raíz del repositorio (PasheCPE.slnx).");
    }

    public static XElement Cargar(string ruta) => XDocument.Load(ruta, LoadOptions.None).Root!;

    public static (EmisorCpe Emisor, ComprobanteCpe Comprobante) Invoice(XElement raiz)
    {
        var proveedor = raiz.Element(_cac + "AccountingSupplierParty")!.Element(_cac + "Party")!;
        var entidad = proveedor.Element(_cac + "PartyLegalEntity")!;
        var domicilio = entidad.Element(_cac + "RegistrationAddress")!;
        var emisor = new EmisorCpe
        {
            Ruc = Texto(proveedor, _cac + "PartyIdentification", _cbc + "ID")!,
            RazonSocial = Texto(entidad, _cbc + "RegistrationName")!,
            NombreComercial = Texto(proveedor, _cac + "PartyName", _cbc + "Name"),
            Domicilio = new DireccionCpe
            {
                Direccion = Texto(domicilio, _cac + "AddressLine", _cbc + "Line")!,
                Ubigeo = Texto(domicilio, _cbc + "ID"),
                Urbanizacion = Texto(domicilio, _cbc + "CitySubdivisionName"),
                Provincia = Texto(domicilio, _cbc + "CityName"),
                Departamento = Texto(domicilio, _cbc + "CountrySubentity"),
                Distrito = Texto(domicilio, _cbc + "District"),
                CodigoPais = Texto(domicilio, _cac + "Country", _cbc + "IdentificationCode") ?? "PE",
            },
            CodigoEstablecimiento = Texto(domicilio, _cbc + "AddressTypeCode") ?? "0000",
            Canal = CanalEnvio.Sunat,
            Certificado = new CertificadoCpe("certificado.pfx", "sin-clave"),
        };

        var cliente = raiz.Element(_cac + "AccountingCustomerParty")!.Element(_cac + "Party")!;
        var idCliente = cliente.Element(_cac + "PartyIdentification")!.Element(_cbc + "ID")!;
        var tipo = raiz.Element(_cbc + "InvoiceTypeCode")!;
        var (serie, numero) = SerieNumero(Texto(raiz, _cbc + "ID")!);
        var pago = raiz.Elements(_cac + "PaymentTerms").FirstOrDefault(p => Texto(p, _cbc + "ID") == "FormaPago");

        var comprobante = new ComprobanteCpe
        {
            TipoDocumento = tipo.Value.Trim(),
            TipoOperacion = (string?)tipo.Attribute("listID") ?? TipoOperacion.VentaInterna,
            Serie = serie,
            Numero = numero,
            FechaEmision = DateOnly.Parse(Texto(raiz, _cbc + "IssueDate")!, CultureInfo.InvariantCulture),
            HoraEmision = TimeOnly.Parse(Texto(raiz, _cbc + "IssueTime") ?? "00:00:00", CultureInfo.InvariantCulture),
            Moneda = Texto(raiz, _cbc + "DocumentCurrencyCode")!,
            ModoPrecio = ModoPrecio.PrecioUnitario,
            Cliente = new ClienteCpe
            {
                TipoDocumento = (string)idCliente.Attribute("schemeID")!,
                NumeroDocumento = idCliente.Value.Trim(),
                RazonSocial = Texto(cliente, _cac + "PartyLegalEntity", _cbc + "RegistrationName")!,
            },
            FormaPago = Texto(pago, _cbc + "PaymentMeansID") == "Credito" ? null : FormaPagoCpe.Contado,
            Lineas = [.. raiz.Elements(_cac + "InvoiceLine").Select(Linea)],
        };

        return (emisor, comprobante);
    }

    private static LineaCpe Linea(XElement linea)
    {
        var cantidad = linea.Element(_cbc + "InvoicedQuantity")!;
        var precio = linea.Element(_cac + "PricingReference")!.Element(_cac + "AlternativeConditionPrice")!;
        var categoria = linea.Element(_cac + "TaxTotal")!.Element(_cac + "TaxSubtotal")!.Element(_cac + "TaxCategory")!;
        var item = linea.Element(_cac + "Item")!;
        return new LineaCpe
        {
            Descripcion = Texto(item, _cbc + "Description")!,
            CodigoInterno = Texto(item, _cac + "SellersItemIdentification", _cbc + "ID"),
            Gtin = Texto(item, _cac + "StandardItemIdentification", _cbc + "ID"),
            CodigoProductoSunat = Texto(item, _cac + "CommodityClassification", _cbc + "ItemClassificationCode"),
            Cantidad = Numero(cantidad.Value),
            Unidad = (string)cantidad.Attribute("unitCode")!,
            ImporteUnitario = Numero(Texto(precio, _cbc + "PriceAmount")!),
            EsGratuito = Texto(precio, _cbc + "PriceTypeCode") == TipoPrecio.ValorReferencialGratuito,
            Afectacion = Texto(categoria, _cbc + "TaxExemptionReasonCode"),
        };
    }

    private static (string Serie, long Numero) SerieNumero(string id)
    {
        var partes = id.Split('-');
        return (partes[0], long.Parse(partes[1], CultureInfo.InvariantCulture));
    }

    private static decimal Numero(string texto) => decimal.Parse(texto.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string? Texto(XElement? elemento, params XName[] ruta)
    {
        foreach (var nombre in ruta)
        {
            elemento = elemento?.Element(nombre);
        }

        return elemento?.Value.Trim();
    }
}
