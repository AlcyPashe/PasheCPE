using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Empaquetado;
using PasheCPE.Respuestas;
using PasheCPE.Tributos;

namespace PasheCPE.Pdf.Representacion;

/// <summary>Resultado de leer un documento para imprimirlo: el comprobante o la guía, o el error.</summary>
internal sealed record LecturaRepresentacion(ComprobanteImpreso? Comprobante, GuiaImpresa? Guia, ErrorCpe? Error);

/// <summary>
/// Lee el <b>XML firmado</b> de <c>FIRMA\yyyy\MM</c> y, si existe, el CDR de <c>RPTA\yyyy\MM</c> → modelo de impresión
/// (TRD §22). Así el PDF refleja exactamente lo enviado y puede regenerarse en cualquier momento. Se lee con LINQ to
/// XML y no con los serializadores UBL, para aceptar también los XML de otros sistemas (dorados de Fattura y
/// Oxigenio). El QR del comprobante se reconstruye desde el XML con las mismas reglas que
/// <see cref="Qr.ConstructorContenidoQr"/>; el de la guía es el enlace que SUNAT devuelve en el CDR. Nunca lanza
/// por un XML ilegible: devuelve <c>PCPE-5003</c>.
/// </summary>
internal sealed class LectorRepresentacion(AlmacenCpe almacen, CatalogosSunat catalogos, CatalogoErrores errores, IHistorialCpe historial, IOptions<OpcionesPasheCpe> opciones)
{
    private static readonly XNamespace _cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private static readonly XNamespace _cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private static readonly XNamespace _ds = "http://www.w3.org/2000/09/xmldsig#";

    public async Task<LecturaRepresentacion> LeerAsync(ReferenciaCpe referencia, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        if (!FormatoImpresion.TieneDiseno(referencia.TipoDocumento))
        {
            return Fallo("PCPE-5004", referencia.TipoDocumento);
        }

        var ruta = almacen.Ruta(CarpetaCpe.Firma, referencia, NombreArchivoCpe.Xml(referencia));
        var xml = await AlmacenCpe.LeerAsync(ruta, ct).ConfigureAwait(false);
        if (xml is null)
        {
            return Fallo("PCPE-5002", ruta);
        }

        var cdr = await AlmacenCpe.LeerAsync(almacen.Ruta(CarpetaCpe.Rpta, referencia, NombreArchivoCpe.Cdr(referencia)), ct).ConfigureAwait(false);
        var estado = (await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false))?.EstadoActual;
        return Leer(referencia, xml, cdr, estado);
    }

    /// <summary>Lee el XML firmado y el ZIP del CDR ya cargados (lo usan las pruebas con los dorados).</summary>
    public LecturaRepresentacion Leer(ReferenciaCpe referencia, byte[] xml, byte[]? cdrZip, EstadoCpe? estadoHistorial)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        ArgumentNullException.ThrowIfNull(xml);
        if (!FormatoImpresion.TieneDiseno(referencia.TipoDocumento))
        {
            return Fallo("PCPE-5004", referencia.TipoDocumento);
        }

        XElement raiz;
        try
        {
            raiz = Cargar(xml);
        }
        catch (XmlException ex)
        {
            return Fallo("PCPE-5003", referencia.NombreArchivo, ex.Message);
        }

        var cdr = cdrZip is null ? null : LectorCdr.Leer(cdrZip, verificarFirma: false);
        try
        {
            return raiz.Name.LocalName switch
            {
                "Invoice" or "CreditNote" or "DebitNote" => new LecturaRepresentacion(Comprobante(referencia, raiz, Estado(cdr, estadoHistorial)), null, null),
                "DespatchAdvice" when cdr?.EnlaceQr is { Length: > 0 } enlace => new LecturaRepresentacion(null, Guia(referencia, raiz, enlace, Estado(cdr, estadoHistorial)), null),
                "DespatchAdvice" => Fallo("PCPE-5005", referencia.NombreArchivo),
                _ => Fallo("PCPE-5003", referencia.NombreArchivo, $"el elemento raíz {raiz.Name.LocalName} no es un comprobante ni una guía"),
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or NullReferenceException or InvalidOperationException)
        {
            return Fallo("PCPE-5003", referencia.NombreArchivo, ex.Message);
        }
    }

    private LecturaRepresentacion Fallo(string codigo, params object?[] argumentos) =>
        new(null, null, errores.Local(codigo, OrigenError.Pdf, null, null, argumentos));

    private static XElement Cargar(byte[] xml)
    {
        var ajustes = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var lector = XmlReader.Create(new MemoryStream(xml, writable: false), ajustes);
        return XDocument.Load(lector, LoadOptions.None).Root ?? throw new XmlException("El XML no tiene elemento raíz.");
    }

    private ComprobanteImpreso Comprobante(ReferenciaCpe referencia, XElement raiz, EstadoSunatImpreso? estado)
    {
        var (tipo, elementoLinea, elementoCantidad, elementoTotales) = raiz.Name.LocalName switch
        {
            "CreditNote" => (TipoDocumentoSunat.NotaDeCredito, "CreditNoteLine", "CreditedQuantity", "LegalMonetaryTotal"),
            "DebitNote" => (TipoDocumentoSunat.NotaDeDebito, "DebitNoteLine", "DebitedQuantity", "RequestedMonetaryTotal"),
            _ => (Texto(raiz, _cbc + "InvoiceTypeCode")!, "InvoiceLine", "InvoicedQuantity", "LegalMonetaryTotal"),
        };
        var moneda = Texto(raiz, _cbc + "DocumentCurrencyCode")!;
        var serieNumero = Texto(raiz, _cbc + "ID")!;
        var fecha = Fecha(Texto(raiz, _cbc + "IssueDate"))!.Value;
        var emisor = Parte(raiz.Element(_cac + "AccountingSupplierParty")?.Element(_cac + "Party"))!;
        var cliente = Parte(raiz.Element(_cac + "AccountingCustomerParty")?.Element(_cac + "Party"))!;
        var totales = Totales(raiz, elementoTotales);
        var valorResumen = raiz.Descendants(_ds + "DigestValue").FirstOrDefault()?.Value.Trim();

        var terminos = raiz.Elements(_cac + "PaymentTerms").ToList();
        var formaPago = terminos.FirstOrDefault(t => Texto(t, _cbc + "ID") == "FormaPago");
        var discrepancia = raiz.Element(_cac + "DiscrepancyResponse");
        var codigoMotivo = Texto(discrepancia, _cbc + "ResponseCode");
        var modificado = raiz.Element(_cac + "BillingReference")?.Element(_cac + "InvoiceDocumentReference");

        return new ComprobanteImpreso
        {
            Referencia = referencia,
            TipoDocumento = tipo,
            SerieNumero = serieNumero,
            FechaEmision = fecha,
            HoraEmision = Hora(Texto(raiz, _cbc + "IssueTime")),
            FechaVencimiento = Fecha(Texto(raiz, _cbc + "DueDate")),
            Moneda = moneda,
            TipoOperacion = (string?)raiz.Element(_cbc + "InvoiceTypeCode")?.Attribute("listID"),
            Emisor = emisor,
            Cliente = cliente,
            FormaPago = Texto(formaPago, _cbc + "PaymentMeansID") switch
            {
                "Contado" => "Contado",
                "Credito" => "Crédito",
                _ => null,
            },
            MontoPendiente = Decimal(Texto(formaPago, _cbc + "Amount")),
            Cuotas =
            [
                .. terminos
                    .Where(t => Texto(t, _cbc + "ID") == "FormaPago" && Texto(t, _cbc + "PaymentMeansID")?.StartsWith("Cuota", StringComparison.Ordinal) == true)
                    .Select(t => new CuotaImpresa(Texto(t, _cbc + "PaymentMeansID")!, Decimal(Texto(t, _cbc + "Amount")) ?? 0m, Fecha(Texto(t, _cbc + "PaymentDueDate")))),
            ],
            DocumentoModificado = modificado is null ? null : new DocumentoRelacionadoImpreso(Texto(modificado, _cbc + "DocumentTypeCode") ?? string.Empty, Texto(modificado, _cbc + "ID")!),
            CodigoMotivo = codigoMotivo,
            Motivo = codigoMotivo is null ? null : catalogos.Descripcion(tipo == TipoDocumentoSunat.NotaDeDebito ? MotivoNotaDebito.NumeroCatalogo : MotivoNotaCredito.NumeroCatalogo, codigoMotivo),
            Sustento = Texto(discrepancia, _cbc + "Description"),
            OrdenCompra = Texto(raiz, _cac + "OrderReference", _cbc + "ID"),
            Guias = [.. raiz.Elements(_cac + "DespatchDocumentReference").Select(d => new DocumentoRelacionadoImpreso(Texto(d, _cbc + "DocumentTypeCode") ?? string.Empty, Texto(d, _cbc + "ID")!))],
            Lineas = [.. raiz.Elements(_cac + elementoLinea).Select((l, i) => Linea(l, elementoCantidad, i + 1))],
            Totales = totales,
            Leyendas = Leyendas(raiz, totales.ImporteTotal, moneda),
            Detraccion = Detraccion(raiz, terminos),
            ValorResumen = valorResumen,
            ContenidoQr = ContenidoQr(emisor.NumeroDocumento, tipo, serieNumero, totales, fecha, cliente, valorResumen),
            Estado = estado,
        };
    }

    /// <summary>Mismo contenido que <see cref="Qr.ConstructorContenidoQr"/>, tomado del XML firmado.</summary>
    private string ContenidoQr(string ruc, string tipo, string serieNumero, TotalesImpresos totales, DateOnly fecha, ParteImpresa cliente, string? valorResumen)
    {
        var qr = opciones.Value.Qr;
        var guion = serieNumero.IndexOf('-', StringComparison.Ordinal);
        var numero = long.Parse(serieNumero[(guion + 1)..], NumberStyles.None, CultureInfo.InvariantCulture);
        string[] campos =
        [
            ruc,
            tipo,
            serieNumero[..guion],
            numero.ToString(qr.CerosNumero ? "00000000" : "0", CultureInfo.InvariantCulture),
            totales.Igv.ToString("0.00", CultureInfo.InvariantCulture),
            totales.ImporteTotal.ToString("0.00", CultureInfo.InvariantCulture),
            fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            cliente.TipoDocumento,
            cliente.NumeroDocumento,
            valorResumen ?? string.Empty,
        ];
        var contenido = string.Join('|', campos);
        return qr.SeparadorFinal ? contenido + "|" : contenido;
    }

    private static TotalesImpresos Totales(XElement raiz, string elementoTotales)
    {
        decimal gravado = 0, exonerado = 0, inafecto = 0, exportacion = 0, gratuito = 0, igv = 0, isc = 0, icbper = 0, otros = 0;
        foreach (var subtotal in raiz.Elements(_cac + "TaxTotal").Elements(_cac + "TaxSubtotal"))
        {
            var baseImponible = Decimal(Texto(subtotal, _cbc + "TaxableAmount")) ?? 0m;
            var impuesto = Decimal(Texto(subtotal, _cbc + "TaxAmount")) ?? 0m;
            switch (Texto(subtotal, _cac + "TaxCategory", _cac + "TaxScheme", _cbc + "ID"))
            {
                case TributoSunat.Igv or TributoSunat.Ivap:
                    gravado += baseImponible;
                    igv += impuesto;
                    break;
                case TributoSunat.Isc:
                    isc += impuesto;
                    break;
                case TributoSunat.Icbper:
                    icbper += impuesto;
                    break;
                case TributoSunat.Exportacion:
                    exportacion += baseImponible;
                    break;
                case TributoSunat.Gratuito:
                    gratuito += baseImponible;
                    break;
                case TributoSunat.Exonerado:
                    exonerado += baseImponible;
                    break;
                case TributoSunat.Inafecto:
                    inafecto += baseImponible;
                    break;
                default:
                    otros += impuesto;
                    break;
            }
        }

        // Descuentos y cargos globales (catálogo N.° 53): 02 y 03 descuentos, 49 y 50 cargos. Los anticipos (04–06)
        // se muestran con PrepaidAmount.
        var globales = raiz.Elements(_cac + "AllowanceCharge").ToList();
        var totalesXml = raiz.Element(_cac + elementoTotales);
        return new TotalesImpresos
        {
            Gravado = gravado,
            Exonerado = exonerado,
            Inafecto = inafecto,
            Exportacion = exportacion,
            Gratuito = gratuito,
            Igv = igv,
            Isc = isc,
            Icbper = icbper,
            OtrosTributos = otros,
            Descuentos = SumaGlobales(globales, cargo: false, CargoDescuento.DescuentoGlobalAfectaBase, CargoDescuento.DescuentoGlobalNoAfectaBase),
            Cargos = SumaGlobales(globales, cargo: true, CargoDescuento.CargoGlobalAfectaBase, CargoDescuento.CargoGlobalNoAfectaBase),
            Anticipos = Decimal(Texto(totalesXml, _cbc + "PrepaidAmount")) ?? 0m,
            Redondeo = Decimal(Texto(totalesXml, _cbc + "PayableRoundingAmount")) ?? 0m,
            ImporteTotal = Decimal(Texto(totalesXml, _cbc + "PayableAmount")) ?? 0m,
        };
    }

    private static decimal SumaGlobales(IEnumerable<XElement> globales, bool cargo, params string[] codigos) =>
        globales
            .Where(g => string.Equals(Texto(g, _cbc + "ChargeIndicator"), cargo ? "true" : "false", StringComparison.OrdinalIgnoreCase)
                && codigos.Contains(Texto(g, _cbc + "AllowanceChargeReasonCode")))
            .Sum(g => Decimal(Texto(g, _cbc + "Amount")) ?? 0m);

    private static LineaImpresa Linea(XElement linea, string elementoCantidad, int posicion)
    {
        var cantidad = linea.Element(_cbc + elementoCantidad);
        var precio = linea.Element(_cac + "PricingReference")?.Element(_cac + "AlternativeConditionPrice");
        var subtotales = linea.Elements(_cac + "TaxTotal").Elements(_cac + "TaxSubtotal").ToList();
        var item = linea.Element(_cac + "Item");
        var descripcion = string.Join(' ', item?.Elements(_cbc + "Description").Select(d => d.Value.Trim()) ?? []);
        return new LineaImpresa
        {
            Numero = int.TryParse(Texto(linea, _cbc + "ID"), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? numero : posicion,
            Codigo = Texto(item, _cac + "SellersItemIdentification", _cbc + "ID"),
            Descripcion = descripcion,
            Unidad = (string?)cantidad?.Attribute("unitCode") ?? string.Empty,
            Cantidad = Decimal(cantidad?.Value) ?? 0m,
            ValorUnitario = Decimal(Texto(linea, _cac + "Price", _cbc + "PriceAmount")) ?? 0m,
            PrecioUnitario = Decimal(Texto(precio, _cbc + "PriceAmount")) ?? 0m,
            Descuento = linea.Elements(_cac + "AllowanceCharge")
                .Where(a => string.Equals(Texto(a, _cbc + "ChargeIndicator"), "false", StringComparison.OrdinalIgnoreCase))
                .Sum(a => Decimal(Texto(a, _cbc + "Amount")) ?? 0m),
            ValorVenta = Decimal(Texto(linea, _cbc + "LineExtensionAmount")) ?? 0m,
            Afectacion = subtotales.Select(s => Texto(s, _cac + "TaxCategory", _cbc + "TaxExemptionReasonCode")).FirstOrDefault(c => c is not null),
            EsGratuita = Texto(precio, _cbc + "PriceTypeCode") == TipoPrecio.ValorReferencialGratuito,
            BolsasIcbper = subtotales
                .Where(s => Texto(s, _cac + "TaxCategory", _cac + "TaxScheme", _cbc + "ID") == TributoSunat.Icbper)
                .Sum(s => Decimal(Texto(s, _cbc + "BaseUnitMeasure")) ?? 0m),
        };
    }

    /// <summary>Leyendas del XML (cbc:Note con languageLocaleID); si falta la 1000, se arma con el importe total.</summary>
    private static List<LeyendaImpresa> Leyendas(XElement raiz, decimal importeTotal, string moneda)
    {
        var leyendas = raiz.Elements(_cbc + "Note")
            .Where(n => n.Attribute("languageLocaleID") is not null)
            .Select(n => new LeyendaImpresa((string)n.Attribute("languageLocaleID")!, n.Value.Trim()))
            .ToList();
        if (!leyendas.Exists(l => l.Codigo == CodigoLeyenda.MontoEnLetras))
        {
            leyendas.Insert(0, new LeyendaImpresa(CodigoLeyenda.MontoEnLetras, NumeroALetras.Convertir(importeTotal, moneda, conTildes: true)));
        }

        return leyendas;
    }

    private DetraccionImpresa? Detraccion(XElement raiz, IReadOnlyList<XElement> terminos)
    {
        var termino = terminos.FirstOrDefault(t => Texto(t, _cbc + "ID") == "Detraccion");
        if (termino is null)
        {
            return null;
        }

        var medio = raiz.Elements(_cac + "PaymentMeans").FirstOrDefault(m => Texto(m, _cbc + "ID") == "Detraccion");
        var codigo = Texto(termino, _cbc + "PaymentMeansID");
        var codigoMedio = Texto(medio, _cbc + "PaymentMeansCode");
        return new DetraccionImpresa(
            codigo,
            codigo is null ? null : catalogos.Descripcion(BienServicioDetraccion.NumeroCatalogo, codigo),
            Decimal(Texto(termino, _cbc + "PaymentPercent")),
            Decimal(Texto(termino, _cbc + "Amount")),
            Texto(medio, _cac + "PayeeFinancialAccount", _cbc + "ID"),
            codigoMedio is null ? null : catalogos.Descripcion(MedioPago.NumeroCatalogo, codigoMedio));
    }

    private GuiaImpresa Guia(ReferenciaCpe referencia, XElement raiz, string enlaceQr, EstadoSunatImpreso? estado)
    {
        var envio = raiz.Element(_cac + "Shipment");
        var tramo = envio?.Element(_cac + "ShipmentStage");
        var entrega = envio?.Element(_cac + "Delivery");
        var peso = envio?.Element(_cbc + "GrossWeightMeasure");
        var codigoMotivo = Texto(envio, _cbc + "HandlingCode");
        var codigoModalidad = Texto(tramo, _cbc + "TransportModeCode");
        var transportista = tramo?.Element(_cac + "CarrierParty");
        var equipos = envio?.Elements(_cac + "TransportHandlingUnit").Elements(_cac + "TransportEquipment").ToList() ?? [];

        return new GuiaImpresa
        {
            Referencia = referencia,
            SerieNumero = Texto(raiz, _cbc + "ID")!,
            FechaEmision = Fecha(Texto(raiz, _cbc + "IssueDate"))!.Value,
            HoraEmision = Hora(Texto(raiz, _cbc + "IssueTime")),
            Remitente = Parte(raiz.Element(_cac + "DespatchSupplierParty")?.Element(_cac + "Party"))!,
            Destinatario = Parte(raiz.Element(_cac + "DeliveryCustomerParty")?.Element(_cac + "Party"))!,
            Comprador = Parte(raiz.Element(_cac + "BuyerCustomerParty")?.Element(_cac + "Party")),
            Proveedor = Parte(raiz.Element(_cac + "SellerSupplierParty")?.Element(_cac + "Party")),
            CodigoMotivo = codigoMotivo,
            Motivo = (codigoMotivo is null ? null : catalogos.Descripcion(MotivoTraslado.NumeroCatalogo, codigoMotivo)) ?? Texto(envio, _cbc + "HandlingInstructions"),
            FechaTraslado = Fecha(Texto(tramo, _cac + "TransitPeriod", _cbc + "StartDate")),
            CodigoModalidad = codigoModalidad,
            Modalidad = codigoModalidad is null ? null : catalogos.Descripcion(ModalidadTraslado.NumeroCatalogo, codigoModalidad),
            PesoBruto = Decimal(peso?.Value),
            UnidadPeso = (string?)peso?.Attribute("unitCode"),
            Bultos = Texto(envio, _cbc + "TotalTransportHandlingUnitQuantity"),
            Partida = Direccion(entrega?.Element(_cac + "Despatch")?.Element(_cac + "DespatchAddress")),
            Llegada = Direccion(entrega?.Element(_cac + "DeliveryAddress")),
            Vehiculos =
            [
                .. equipos.Select(e => Texto(e, _cbc + "ID")).OfType<string>(),
                .. equipos.Elements(_cac + "AttachedTransportEquipment").Select(e => Texto(e, _cbc + "ID")).OfType<string>(),
            ],
            Conductores = [.. tramo?.Elements(_cac + "DriverPerson").Select(Conductor) ?? []],
            Transportista = Parte(transportista),
            DocumentosRelacionados =
            [
                .. raiz.Elements(_cac + "AdditionalDocumentReference").Select(d =>
                {
                    var tipo = Texto(d, _cbc + "DocumentTypeCode");
                    var descripcion = (tipo is null ? null : catalogos.Descripcion(DocumentoRelacionadoTraslado.NumeroCatalogo, tipo)) ?? Texto(d, _cbc + "DocumentType") ?? tipo ?? string.Empty;
                    return new DocumentoRelacionadoImpreso(descripcion, Texto(d, _cbc + "ID")!) { Emisor = Texto(d, _cac + "IssuerParty", _cac + "PartyIdentification", _cbc + "ID") };
                }),
            ],
            Indicadores = [.. envio?.Elements(_cbc + "SpecialInstructions").Select(i => i.Value.Trim()) ?? []],
            Bienes = [.. raiz.Elements(_cac + "DespatchLine").Select((l, i) => Bien(l, i + 1))],
            Observaciones = Texto(raiz, _cbc + "Note"),
            ValorResumen = raiz.Descendants(_ds + "DigestValue").FirstOrDefault()?.Value.Trim(),
            EnlaceQr = enlaceQr,
            Estado = estado,
        };
    }

    private static ConductorImpreso Conductor(XElement conductor)
    {
        var id = conductor.Element(_cbc + "ID");
        var nombre = string.Join(' ', new[] { Texto(conductor, _cbc + "FirstName"), Texto(conductor, _cbc + "FamilyName") }.Where(n => !string.IsNullOrEmpty(n)));
        return new ConductorImpreso(
            (string?)id?.Attribute("schemeID") ?? string.Empty,
            id?.Value.Trim() ?? string.Empty,
            nombre,
            Texto(conductor, _cac + "IdentityDocumentReference", _cbc + "ID"),
            Texto(conductor, _cbc + "JobTitle") != "Secundario");
    }

    private static BienImpreso Bien(XElement linea, int posicion)
    {
        var cantidad = linea.Element(_cbc + "DeliveredQuantity");
        var item = linea.Element(_cac + "Item");
        return new BienImpreso(
            int.TryParse(Texto(linea, _cbc + "ID"), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? numero : posicion,
            Texto(item, _cac + "SellersItemIdentification", _cbc + "ID"),
            string.Join(' ', item?.Elements(_cbc + "Description").Select(d => d.Value.Trim()) ?? []),
            (string?)cantidad?.Attribute("unitCode") ?? string.Empty,
            Decimal(cantidad?.Value) ?? 0m);
    }

    /// <summary>Emisor, cliente o transportista: documento, razón social, nombre comercial y dirección.</summary>
    private static ParteImpresa? Parte(XElement? parte)
    {
        if (parte is null)
        {
            return null;
        }

        var id = parte.Element(_cac + "PartyIdentification")?.Element(_cbc + "ID");
        var entidad = parte.Element(_cac + "PartyLegalEntity");
        return new ParteImpresa(
            (string?)id?.Attribute("schemeID") ?? string.Empty,
            id?.Value.Trim() ?? string.Empty,
            Texto(entidad, _cbc + "RegistrationName") ?? Texto(parte, _cac + "PartyName", _cbc + "Name") ?? string.Empty)
        {
            NombreComercial = Texto(parte, _cac + "PartyName", _cbc + "Name"),
            Direccion = Domicilio(entidad?.Element(_cac + "RegistrationAddress")),
            RegistroMtc = Texto(entidad, _cbc + "CompanyID"),
        };
    }

    /// <summary>«JR. PRÓSPERO 100, IQUITOS, MAYNAS, LORETO».</summary>
    private static string? Domicilio(XElement? direccion)
    {
        if (direccion is null)
        {
            return null;
        }

        string?[] partes =
        [
            Texto(direccion, _cac + "AddressLine", _cbc + "Line"),
            Texto(direccion, _cbc + "CitySubdivisionName"),
            Texto(direccion, _cbc + "District"),
            Texto(direccion, _cbc + "CityName"),
            Texto(direccion, _cbc + "CountrySubentity"),
        ];
        var texto = string.Join(", ", partes.Where(p => !string.IsNullOrWhiteSpace(p) && p != "-"));
        return texto.Length == 0 ? null : texto;
    }

    /// <summary>Partida o llegada de la guía: «AV. AUGUSTO FREYRE 727 (UBIGEO 160101)».</summary>
    private static string? Direccion(XElement? direccion)
    {
        var linea = Texto(direccion, _cac + "AddressLine", _cbc + "Line");
        var ubigeo = Texto(direccion, _cbc + "ID");
        return (linea, ubigeo) switch
        {
            (null, null) => null,
            (_, null) => linea,
            (null, _) => $"UBIGEO {ubigeo}",
            _ => $"{linea} (UBIGEO {ubigeo})",
        };
    }

    /// <summary>
    /// Estado para el PDF: dado de baja según el historial; si no, la descripción del CDR (con «observaciones» si trae
    /// notas); sin CDR, el estado aceptado del historial (boletas informadas en el resumen diario).
    /// </summary>
    private static EstadoSunatImpreso? Estado(CdrLeido? cdr, EstadoCpe? estadoHistorial)
    {
        if (estadoHistorial == EstadoCpe.DeBaja)
        {
            return new EstadoSunatImpreso("Comunicado de baja", null);
        }

        if (cdr is not null)
        {
            var texto = cdr.Descripcion ?? $"Código {cdr.Codigo}";
            return new EstadoSunatImpreso(cdr.Notas.Count > 0 ? $"{texto} (con observaciones)" : texto, cdr.Fecha);
        }

        return estadoHistorial switch
        {
            EstadoCpe.Aceptado => new EstadoSunatImpreso("Aceptado", null),
            EstadoCpe.AceptadoConObservaciones => new EstadoSunatImpreso("Aceptado con observaciones", null),
            _ => null,
        };
    }

    private static string? Texto(XElement? elemento, params XName[] ruta)
    {
        foreach (var nombre in ruta)
        {
            elemento = elemento?.Element(nombre);
        }

        var texto = elemento?.Value.Trim();
        return string.IsNullOrEmpty(texto) ? null : texto;
    }

    private static decimal? Decimal(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : decimal.Parse(texto.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static DateOnly? Fecha(string? texto) =>
        texto is null ? null : DateOnly.ParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static TimeOnly? Hora(string? texto) =>
        texto is not null && TimeOnly.TryParse(texto.Length > 8 ? texto[..8] : texto, CultureInfo.InvariantCulture, out var hora) ? hora : null;
}
