using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Empaquetado;
using PasheCPE.Ubl;

namespace PasheCPE.Mapeo;

/// <summary>Línea del resumen leída del XML firmado, con la fecha de emisión del documento, o el error que lo impidió.</summary>
internal sealed record LineaLeida(LineaResumenCpe? Linea, DateOnly? FechaEmision, ErrorCpe? Error);

/// <summary>
/// Arma la línea del resumen diario de una boleta o nota leyendo su XML firmado de <c>FIRMA\yyyy\MM</c>, así lo que se
/// informa coincide con lo firmado: cliente, moneda, totales por tributo (1000, 9997, 9998, 9995, 2000, 7152), las
/// gratuitas por afectación de cada línea (11–17 gravadas, 21 exoneradas, 31–37 inafectas), el IGV de las gratuitas, la
/// tasa del IGV, otros cargos, el importe total y, en las notas, la boleta que modifican. Nunca lanza.
/// </summary>
internal sealed class LectorBoletaFirmada(AlmacenCpe almacen, CatalogoErrores errores)
{
    private static readonly XNamespace _cac = EspaciosUbl.Cac;
    private static readonly XNamespace _cbc = EspaciosUbl.Cbc;

    public async Task<LineaLeida> LeerAsync(ReferenciaCpe referencia, string estado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        var ruta = almacen.Ruta(CarpetaCpe.Firma, referencia, NombreArchivoCpe.Xml(referencia));
        var contenido = await AlmacenCpe.LeerAsync(ruta, ct).ConfigureAwait(false);
        if (contenido is null)
        {
            return new LineaLeida(null, null, errores.Local("PCPE-1316", OrigenError.Modelo, null, null, referencia.NombreArchivo));
        }

        try
        {
            return Leer(referencia, estado, contenido);
        }
        catch (Exception ex) when (ex is XmlException or FormatException or OverflowException or InvalidDataException)
        {
            return new LineaLeida(null, null, errores.Local("PCPE-1317", OrigenError.Modelo, null, null, referencia.NombreArchivo, ex.Message));
        }
    }

    private static LineaLeida Leer(ReferenciaCpe referencia, string estado, byte[] contenido)
    {
        XElement raiz;
        using (var memoria = new MemoryStream(contenido, writable: false))
        using (var lector = XmlReader.Create(memoria, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            raiz = XDocument.Load(lector).Root ?? throw new InvalidDataException("el XML no tiene elemento raíz.");
        }

        var fecha = DateOnly.ParseExact(Texto(raiz, _cbc + "IssueDate") ?? throw new InvalidDataException("falta cbc:IssueDate."), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var parte = raiz.Element(_cac + "AccountingCustomerParty")?.Element(_cac + "Party");
        var identificacion = parte?.Element(_cac + "PartyIdentification")?.Element(_cbc + "ID");
        var cliente = new ClienteCpe
        {
            TipoDocumento = identificacion?.Attribute("schemeID")?.Value ?? "-",
            NumeroDocumento = identificacion?.Value ?? "-",
            RazonSocial = parte?.Element(_cac + "PartyLegalEntity")?.Element(_cbc + "RegistrationName")?.Value ?? string.Empty,
        };

        var subtotales = raiz.Elements(_cac + "TaxTotal").Elements(_cac + "TaxSubtotal").ToList();
        decimal Base(string tributo) => subtotales.Where(s => Tributo(s) == tributo).Sum(s => Monto(s, _cbc + "TaxableAmount"));
        decimal Impuesto(string tributo) => subtotales.Where(s => Tributo(s) == tributo).Sum(s => Monto(s, _cbc + "TaxAmount"));

        // Las gratuitas se desagregan por la afectación de cada línea (desde 2027 se informan por separado).
        var gratuitasPorLinea = raiz.Elements().Where(e => e.Name.LocalName.EndsWith("Line", StringComparison.Ordinal))
            .SelectMany(l => l.Elements(_cac + "TaxTotal").Elements(_cac + "TaxSubtotal"))
            .Where(s => Tributo(s) == TributoSunat.Gratuito)
            .Select(s => (Afectacion: s.Element(_cac + "TaxCategory")?.Element(_cbc + "TaxExemptionReasonCode")?.Value ?? string.Empty, Base: Monto(s, _cbc + "TaxableAmount")))
            .ToList();
        decimal Gratuito(Func<string, bool> grupo) => gratuitasPorLinea.Where(g => grupo(g.Afectacion)).Sum(g => g.Base);

        var lineasIgv = raiz.Descendants(_cac + "TaxSubtotal")
            .Where(s => Tributo(s) == TributoSunat.Igv || (Tributo(s) == TributoSunat.Gratuito && EsGravada(Afectacion(s))))
            .Select(s => s.Element(_cac + "TaxCategory")?.Element(_cbc + "Percent")?.Value)
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
        var totales = raiz.Element(_cac + "LegalMonetaryTotal") ?? raiz.Element(_cac + "RequestedMonetaryTotal");
        var modificado = raiz.Element(_cac + "BillingReference")?.Element(_cac + "InvoiceDocumentReference");

        var linea = new LineaResumenCpe
        {
            TipoDocumento = referencia.TipoDocumento,
            Serie = referencia.Serie!,
            Numero = referencia.Numero!.Value,
            Estado = estado,
            Cliente = cliente,
            Moneda = Texto(raiz, _cbc + "DocumentCurrencyCode") ?? "PEN",
            DocumentoModificado = modificado is null ? null : Modificado(modificado),
            TotalGravado = Base(TributoSunat.Igv),
            TotalExonerado = Base(TributoSunat.Exonerado),
            TotalInafecto = Base(TributoSunat.Inafecto),
            TotalExportacion = Base(TributoSunat.Exportacion),
            TotalGratuitoGravado = Gratuito(EsGravada),
            TotalGratuitoExonerado = Gratuito(a => a == "21"),
            TotalGratuitoInafecto = Gratuito(a => a.StartsWith('3')),
            OtrosCargos = Monto(totales, _cbc + "ChargeTotalAmount"),
            Igv = Impuesto(TributoSunat.Igv),
            IgvGratuitas = Impuesto(TributoSunat.Gratuito),
            TasaIgv = lineasIgv is null ? null : decimal.Parse(lineasIgv, NumberStyles.Number, CultureInfo.InvariantCulture),
            Isc = Impuesto(TributoSunat.Isc),
            Icbper = Impuesto(TributoSunat.Icbper),
            ImporteTotal = Monto(totales, _cbc + "PayableAmount"),
        };
        return new LineaLeida(linea, fecha, null);
    }

    private static DocumentoModificadoCpe Modificado(XElement referencia)
    {
        var id = referencia.Element(_cbc + "ID")?.Value ?? string.Empty;
        var guion = id.LastIndexOf('-');
        if (guion <= 0)
        {
            throw new InvalidDataException($"el documento modificado '{id}' no tiene el formato serie-número.");
        }

        return new DocumentoModificadoCpe
        {
            TipoDocumento = referencia.Element(_cbc + "DocumentTypeCode")?.Value ?? TipoDocumentoSunat.BoletaDeVenta,
            Serie = id[..guion],
            Numero = long.Parse(id[(guion + 1)..], NumberStyles.None, CultureInfo.InvariantCulture),
        };
    }

    private static bool EsGravada(string afectacion) => afectacion.StartsWith('1') && afectacion != "10";

    private static string Afectacion(XElement subtotal) => subtotal.Element(_cac + "TaxCategory")?.Element(_cbc + "TaxExemptionReasonCode")?.Value ?? string.Empty;

    private static string? Tributo(XElement subtotal) =>
        subtotal.Element(_cac + "TaxCategory")?.Element(_cac + "TaxScheme")?.Element(_cbc + "ID")?.Value;

    private static string? Texto(XElement elemento, XName nombre) => elemento.Element(nombre)?.Value;

    private static decimal Monto(XElement? elemento, XName nombre) =>
        elemento?.Element(nombre)?.Value is { Length: > 0 } texto ? decimal.Parse(texto, NumberStyles.Number, CultureInfo.InvariantCulture) : 0m;
}
