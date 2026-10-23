using System.Xml.Linq;
using PasheCPE.Almacenamiento;
using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Tributos;

namespace PasheCPE.Tests.Resumen;

/// <summary>Resumen diario con los totales del modelo: mapeo (incluidos los campos 2027), lotes, correlativos y validación.</summary>
public sealed class ResumenDiarioTests : IDisposable
{
    private static readonly DateOnly _fecha = new(2026, 10, 22);
    private static readonly XNamespace _cbc = XmlDePrueba.Cbc;
    private static readonly XNamespace _cac = XmlDePrueba.Cac;
    private static readonly XNamespace _sac = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerarResumenAsync_Gratuitas2026_SeSumanEnElCodigo05SinIgvDeGratuitas()
    {
        var linea = Linea(1) with { TotalGratuitoGravado = 10m, TotalGratuitoExonerado = 5m, IgvGratuitas = 1.80m };

        var xml = await GenerarUnoAsync(_servicios, linea);

        var resultado = Assert.Single(xml.Lineas);
        Assert.Equal(["01", "05"], Pagos(resultado).Select(p => p.Tipo));
        Assert.Equal(15m, Pagos(resultado).Single(p => p.Tipo == "05").Monto);
        Assert.Equal(["1000"], Tributos(resultado).Select(t => t.Tributo));
    }

    [Fact]
    public async Task GenerarResumenAsync_Gratuitas2027_SeDesagreganYSeInformaElIgvDeGratuitas()
    {
        using var servicios = new ServiciosPrueba(ahora: new DateTimeOffset(2027, 1, 5, 15, 0, 0, TimeSpan.Zero));
        var linea = Linea(1) with { TotalGratuitoGravado = 10m, TotalGratuitoExonerado = 5m, TotalGratuitoInafecto = 2m, IgvGratuitas = 1.80m };

        var xml = await GenerarUnoAsync(servicios, linea, new DateOnly(2027, 1, 4));

        var resultado = Assert.Single(xml.Lineas);
        Assert.Equal(["01", "06", "07", "08"], Pagos(resultado).Select(p => p.Tipo));
        var gratuitas = Tributos(resultado).Single(t => t.Tributo == "9996");
        Assert.Equal((1.80m, "18.00", "GRA"), (gratuitas.Monto, gratuitas.Tasa, gratuitas.Nombre));
    }

    [Fact]
    public async Task GenerarResumenAsync_Exonerada_InformaElIgvEnCeroConTasaYOmiteIscEIcbperEnCero()
    {
        var linea = Linea(1) with { TotalGravado = 0m, Igv = 0m, TotalExonerado = 100m, ImporteTotal = 100m };

        var xml = await GenerarUnoAsync(_servicios, linea);

        var tributo = Assert.Single(Tributos(Assert.Single(xml.Lineas)));
        Assert.Equal(("1000", 0m, "18.00"), (tributo.Tributo, tributo.Monto, tributo.Tasa));
    }

    [Fact]
    public async Task GenerarResumenAsync_IscIcbperYOtrosCargos_SeInformanCuandoSonMayoresQueCero()
    {
        var linea = Linea(1) with { Isc = 5m, Igv = 18.90m, Icbper = 0.50m, OtrosCargos = 2m, ImporteTotal = 126.40m };

        var xml = await GenerarUnoAsync(_servicios, linea);

        var resultado = Assert.Single(xml.Lineas);
        Assert.Equal(["1000", "2000", "7152"], Tributos(resultado).Select(t => t.Tributo));
        Assert.Equal("2.00", resultado.Element(_cac + "AllowanceCharge")!.Element(_cbc + "Amount")!.Value);
    }

    [Fact]
    public async Task GenerarResumenAsync_TasaMype_SeUsaLaTasaEspecialDelEmisor()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen) with { ExoneradoAmazonia = false, TasaEspecialRestaurantesHoteles = true };
        var linea = Linea(1) with { Igv = 10.50m, ImporteTotal = 110.50m };

        var resultado = Assert.Single(await Servicio(_servicios).GenerarResumenAsync(emisor, Resumen(linea), Ct));

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        Assert.Equal("10.50", Tributos(GeneracionResumenTests.Lineas(await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct))[0]).Single().Tasa);
    }

    [Fact]
    public async Task GenerarResumenAsync_NotaDeCredito_InformaLaBoletaQueModifica()
    {
        var nota = Linea(5) with
        {
            TipoDocumento = TipoDocumentoSunat.NotaDeCredito,
            Serie = "BC01",
            DocumentoModificado = new DocumentoModificadoCpe { TipoDocumento = TipoDocumentoSunat.BoletaDeVenta, Serie = "B001", Numero = 1 },
        };

        var xml = await GenerarUnoAsync(_servicios, nota);

        var referencia = Assert.Single(xml.Lineas).Element(_cac + "BillingReference")!.Element(_cac + "InvoiceDocumentReference")!;
        Assert.Equal(("B001-1", "03"), (referencia.Element(_cbc + "ID")!.Value, referencia.Element(_cbc + "DocumentTypeCode")!.Value));
    }

    [Fact]
    public async Task GenerarResumenAsync_ClienteIdentificado_InformaSuRazonSocial()
    {
        var xml = await GenerarUnoAsync(_servicios, Linea(1));

        var cliente = Assert.Single(xml.Lineas).Element(_cac + "AccountingCustomerParty")!;
        Assert.Equal(("12345678", "1", "CLIENTE DE PRUEBA"), (
            cliente.Element(_cbc + "CustomerAssignedAccountID")!.Value,
            cliente.Element(_cbc + "AdditionalAccountID")!.Value,
            cliente.Element(_cac + "Party")!.Element(_cac + "PartyLegalEntity")!.Element(_cbc + "RegistrationName")!.Value));
    }

    [Fact]
    public async Task GenerarResumenAsync_MasDe500Lineas_DivideEnLotesConCorrelativosConsecutivos()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var resumen = Resumen([.. Enumerable.Range(1, 1200).Select(n => Linea(n))]);

        var resultados = await Servicio(_servicios).GenerarResumenAsync(emisor, resumen, Ct);

        Assert.All(resultados, r => Assert.True(r.Exito, GeneracionResumenTests.Errores(r)));
        Assert.Equal(["20123456786-RC-20261022-1", "20123456786-RC-20261022-2", "20123456786-RC-20261022-3"], resultados.Select(r => r.NombreArchivo));
        var lineas = await Task.WhenAll(resultados.Select(async r => GeneracionResumenTests.Lineas(await File.ReadAllBytesAsync(r.Rutas.Xml!, Ct))));
        Assert.Equal([500, 500, 200], lineas.Select(l => l.Count));
        Assert.Equal(["1", "500"], new[] { lineas[2][0], lineas[1][499] }.Select(l => l.Element(_cbc + "LineID")!.Value));
        Assert.Equal("B001-1001", lineas[2][0].Element(_cbc + "ID")!.Value);
    }

    [Fact]
    public async Task GenerarResumenAsync_OtroResumenDelDia_ContinuaElCorrelativo()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        await Servicio(_servicios).GenerarResumenAsync(emisor, Resumen(Linea(1)), Ct);

        var segundo = Assert.Single(await Servicio(_servicios).GenerarResumenAsync(emisor, Resumen(Linea(2)), Ct));

        Assert.Equal("20123456786-RC-20261022-2", segundo.NombreArchivo);
    }

    [Fact]
    public async Task GenerarResumenAsync_GeneracionesSimultaneas_NoRepitenElCorrelativo()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultados = await Task.WhenAll(Enumerable.Range(1, 4).Select(n => Servicio(_servicios).GenerarResumenAsync(emisor, Resumen(Linea(n)), Ct)));

        Assert.Equal(4, resultados.Select(r => Assert.Single(r).NombreArchivo).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task GenerarResumenAsync_SinLineas_ErrorSinEscribir()
    {
        var resultado = await GenerarConErrorAsync(Resumen());

        Assert.Equal("PCPE-1301", Assert.Single(resultado.Errores).Codigo);
        Assert.False(Directory.Exists(Path.Combine(_servicios.Almacen.Ruta, "ENVIO")));
    }

    [Theory]
    [InlineData("tipo", "PCPE-1304")]
    [InlineData("serie", "PCPE-1305")]
    [InlineData("negativo", "PCPE-1309")]
    [InlineData("anonimo", "PCPE-1211")]
    [InlineData("boletaConReferencia", "PCPE-1308")]
    [InlineData("notaSinReferencia", "PCPE-1306")]
    [InlineData("estado", "PCPE-1001")]
    public async Task GenerarResumenAsync_LineaInvalida_ErrorDelModelo(string caso, string codigo)
    {
        var modificado = new DocumentoModificadoCpe { TipoDocumento = TipoDocumentoSunat.BoletaDeVenta, Serie = "B001", Numero = 1 };
        var linea = caso switch
        {
            "tipo" => Linea(1) with { TipoDocumento = TipoDocumentoSunat.Factura },
            "serie" => Linea(1) with { Serie = "F001" },
            "negativo" => Linea(1) with { TotalExonerado = -1m },
            "anonimo" => Linea(1) with { Cliente = new ClienteCpe { TipoDocumento = "0", NumeroDocumento = "-", RazonSocial = "CLIENTES VARIOS" }, ImporteTotal = 800m, TotalGravado = 677.97m, Igv = 122.03m },
            "boletaConReferencia" => Linea(1) with { DocumentoModificado = modificado },
            "notaSinReferencia" => Linea(1) with { TipoDocumento = TipoDocumentoSunat.NotaDeCredito, Serie = "BC01" },
            _ => Linea(1) with { Estado = "9" },
        };

        var resultado = await GenerarConErrorAsync(Resumen(linea));

        Assert.Contains(resultado.Errores, e => e.Codigo == codigo);
    }

    [Theory]
    [InlineData(EstadoItemResumen.Adicionar, EstadoItemResumen.Adicionar, "PCPE-1310")]
    [InlineData(EstadoItemResumen.Adicionar, EstadoItemResumen.Modificar, "PCPE-1311")]
    [InlineData(EstadoItemResumen.Modificar, EstadoItemResumen.Anulado, "PCPE-1312")]
    public async Task GenerarResumenAsync_DocumentoRepetido_Error(string primero, string segundo, string codigo)
    {
        var resultado = await GenerarConErrorAsync(Resumen(Linea(1) with { Estado = primero }, Linea(1) with { Estado = segundo }));

        Assert.Contains(resultado.Errores, e => e.Codigo == codigo);
    }

    [Fact]
    public async Task GenerarResumenAsync_FechaDeEmisionPosteriorALaGeneracion_Error2671()
    {
        var resultado = await GenerarConErrorAsync(Resumen(Linea(1)) with { FechaReferencia = new DateOnly(2026, 10, 23), FechaGeneracion = _fecha });

        Assert.Contains(resultado.Errores, e => e.Codigo == "PCPE-1302");
    }

    [Fact]
    public async Task GenerarResumenAsync_FueraDePlazo_SeGeneraConAdvertencia4443()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultado = Assert.Single(await Servicio(_servicios).GenerarResumenAsync(emisor, Resumen(Linea(1)) with { FechaReferencia = new DateOnly(2026, 10, 10) }, Ct));

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        Assert.Contains(resultado.Advertencias, a => a.Codigo == "PCPE-1318");
    }

    [Fact]
    public async Task GenerarResumenAsync_AnulacionFueraDePlazo_Error2957()
    {
        var resultado = await GenerarConErrorAsync(Resumen(Linea(1) with { Estado = EstadoItemResumen.Anulado }) with { FechaReferencia = new DateOnly(2026, 10, 10) });

        Assert.Contains(resultado.Errores, e => e.Codigo == "PCPE-1313");
    }

    [Theory]
    [InlineData(EstadoItemResumen.Adicionar, EstadoCpe.Aceptado, "PCPE-1314")]
    [InlineData(EstadoItemResumen.Adicionar, EstadoCpe.EnProceso, "PCPE-1314")]
    [InlineData(EstadoItemResumen.Modificar, EstadoCpe.Generado, "PCPE-1315")]
    [InlineData(EstadoItemResumen.Anulado, EstadoCpe.Generado, "PCPE-1315")]
    public async Task GenerarResumenAsync_SegunElHistorialDeLaBoleta_Error(string estadoItem, EstadoCpe estadoBoleta, string codigo)
    {
        var boleta = ReferenciaCpe.Comprobante(Ejemplos.Ruc, TipoDocumentoSunat.BoletaDeVenta, "B001", 1, _fecha);
        await _servicios.Obtener<RegistroHistorial>().RegistrarAsync(boleta, estadoBoleta, null, Ct);

        var resultado = await GenerarConErrorAsync(Resumen(Linea(1) with { Estado = estadoItem }));

        Assert.Equal(codigo, Assert.Single(resultado.Errores).Codigo);
    }

    [Fact]
    public async Task GenerarResumenAsync_AnularBoletaAceptada_SeGenera()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var boleta = ReferenciaCpe.Comprobante(Ejemplos.Ruc, TipoDocumentoSunat.BoletaDeVenta, "B001", 1, _fecha);
        await _servicios.Obtener<RegistroHistorial>().RegistrarAsync(boleta, EstadoCpe.Aceptado, null, Ct);

        var resultado = Assert.Single(await Servicio(_servicios).GenerarResumenAsync(emisor, Resumen(Linea(1) with { Estado = EstadoItemResumen.Anulado }), Ct));

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        Assert.Equal("3", GeneracionResumenTests.Lineas(await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct))[0].Element(_cac + "Status")!.Element(_cbc + "ConditionCode")!.Value);
    }

    [Fact]
    public async Task GenerarResumenAsync_DesdeFirmaSinXmlFirmado_Error()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultado = Assert.Single(await Servicio(_servicios).GenerarResumenAsync(emisor, _fecha, [GeneracionResumenTests.Documento(9)], Ct));

        Assert.Equal("PCPE-1316", Assert.Single(resultado.Errores).Codigo);
    }

    [Theory]
    [InlineData("BoletaGravada")]
    [InlineData("BoletaMypeDiezYMedio")]
    [InlineData("BoletaExonerada")]
    [InlineData("BoletaClienteIdentificado")]
    [InlineData("GratuitaGravada")]
    [InlineData("GratuitaExonerada")]
    [InlineData("Icbper")]
    [InlineData("Mixta")]
    public async Task LeerAsync_BoletaFirmada_CoincideConElCalculo(string nombre)
    {
        var caso = CasosTributarios.Todos[nombre];
        var cpe = caso.Comprobante.TipoDocumento == TipoDocumentoSunat.BoletaDeVenta
            ? caso.Comprobante
            : caso.Comprobante with { TipoDocumento = TipoDocumentoSunat.BoletaDeVenta, Serie = "B001", Cliente = CasosTributarios.Boleta().Cliente };
        var emisor = caso.Emisor with { Certificado = CertificadoPrueba.Crear(_servicios.Almacen) };
        var generado = await Servicio(_servicios).GenerarAsync(emisor, cpe, Ct);
        Assert.True(generado.Exito, GeneracionResumenTests.Errores(generado));
        var calculo = _servicios.Obtener<CalculadoraTributaria>().Calcular(emisor, cpe);

        var leida = await _servicios.Obtener<LectorBoletaFirmada>().LeerAsync(generado.Referencia, EstadoItemResumen.Adicionar, Ct);

        var linea = leida.Linea!;
        decimal Base(string tributo) => calculo.Subtotales.Where(s => s.Tributo == tributo).Sum(s => s.Base);
        decimal Monto(string tributo) => calculo.Subtotales.Where(s => s.Tributo == tributo).Sum(s => s.Monto);
        Assert.Equal(cpe.FechaEmision, leida.FechaEmision);
        Assert.Equal((Base("1000"), Monto("1000")), (linea.TotalGravado, linea.Igv));
        Assert.Equal((Base("9997"), Base("9998")), (linea.TotalExonerado, linea.TotalInafecto));
        Assert.Equal((Base("9996"), Monto("9996")), (linea.TotalGratuitoGravado + linea.TotalGratuitoExonerado + linea.TotalGratuitoInafecto, linea.IgvGratuitas));
        Assert.Equal((Monto("7152"), calculo.ImporteTotal), (linea.Icbper, linea.ImporteTotal));
        Assert.Equal(cpe.Cliente.NumeroDocumento, linea.Cliente.NumeroDocumento);
        if (calculo.Lineas.Any(l => l.Tributo == TributoSunat.Igv))
        {
            Assert.Equal(calculo.TasaGravada, linea.TasaIgv);
        }

        var resumen = Assert.Single(await Servicio(_servicios).GenerarResumenAsync(emisor, generado.Referencia.Fecha, [GeneracionResumenTests.Documento(cpe.Numero)], Ct));
        Assert.True(resumen.Exito, GeneracionResumenTests.Errores(resumen));
    }

    [Fact]
    public async Task GenerarResumenAsync_MismasLineasQueElDorado_DaLosMismosImportes()
    {
        var dorado = XDocument.Load(Path.Combine(Dorados.LectorDorado.Carpeta("RC-Resumen"), "20611619767-RC-20261022-7.xml")).Root!;
        var lineasDorado = dorado.Elements(_sac + "SummaryDocumentsLine").ToList();
        var lineas = lineasDorado.Select(l => Linea(1) with
        {
            Serie = l.Element(_cbc + "ID")!.Value.Split('-')[0],
            Numero = long.Parse(l.Element(_cbc + "ID")!.Value.Split('-')[1], System.Globalization.CultureInfo.InvariantCulture),
            Cliente = new ClienteCpe
            {
                TipoDocumento = l.Element(_cac + "AccountingCustomerParty")!.Element(_cbc + "AdditionalAccountID")!.Value,
                NumeroDocumento = l.Element(_cac + "AccountingCustomerParty")!.Element(_cbc + "CustomerAssignedAccountID")!.Value,
                RazonSocial = "CLIENTE",
            },
            TotalGravado = Pago(l, "01"),
            TotalExonerado = Pago(l, "02"),
            TotalInafecto = Pago(l, "03"),
            Igv = decimal.Parse(l.Elements(_cac + "TaxTotal").Single(t => t.Descendants(_cbc + "ID").Any(i => i.Value == "1000")).Element(_cbc + "TaxAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture),
            ImporteTotal = decimal.Parse(l.Element(_sac + "TotalAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture),
        }).ToArray();

        var xml = await GenerarUnoAsync(_servicios, lineas);

        Assert.Equal(lineasDorado.Count, xml.Lineas.Count);
        for (var i = 0; i < lineasDorado.Count; i++)
        {
            Assert.Equal(lineasDorado[i].Element(_cbc + "ID")!.Value, xml.Lineas[i].Element(_cbc + "ID")!.Value);
            Assert.Equal(lineasDorado[i].Element(_sac + "TotalAmount")!.Value, xml.Lineas[i].Element(_sac + "TotalAmount")!.Value);
            Assert.Equal(Pagos(lineasDorado[i]), Pagos(xml.Lineas[i]));
        }
    }

    private static decimal Pago(XElement linea, string tipo) =>
        linea.Elements(_sac + "BillingPayment").Where(p => p.Element(_cbc + "InstructionID")!.Value == tipo)
            .Sum(p => decimal.Parse(p.Element(_cbc + "PaidAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture));

    private static IServicioCpe Servicio(ServiciosPrueba servicios) => servicios.Obtener<IServicioCpe>();

    private static LineaResumenCpe Linea(long numero) => new()
    {
        TipoDocumento = TipoDocumentoSunat.BoletaDeVenta,
        Serie = "B001",
        Numero = numero,
        Estado = EstadoItemResumen.Adicionar,
        Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "CLIENTE DE PRUEBA" },
        TotalGravado = 100m,
        Igv = 18m,
        ImporteTotal = 118m,
    };

    private static ResumenDiarioCpe Resumen(params LineaResumenCpe[] lineas) => new() { FechaReferencia = _fecha, Lineas = lineas };

    private static IReadOnlyList<(string Tipo, decimal Monto)> Pagos(XElement linea) =>
        [.. linea.Elements(_sac + "BillingPayment").Select(p => (p.Element(_cbc + "InstructionID")!.Value, decimal.Parse(p.Element(_cbc + "PaidAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture)))];

    private static IReadOnlyList<(string Tributo, decimal Monto, string? Tasa, string? Nombre)> Tributos(XElement linea) =>
        [.. linea.Elements(_cac + "TaxTotal").Select(t =>
        {
            var categoria = t.Element(_cac + "TaxSubtotal")!.Element(_cac + "TaxCategory")!;
            var esquema = categoria.Element(_cac + "TaxScheme")!;
            return (esquema.Element(_cbc + "ID")!.Value, decimal.Parse(t.Element(_cbc + "TaxAmount")!.Value, System.Globalization.CultureInfo.InvariantCulture), categoria.Element(_cbc + "Percent")?.Value, esquema.Element(_cbc + "Name")?.Value);
        })];

    private static async Task<(IReadOnlyList<XElement> Lineas, ResultadoGeneracion Resultado)> GenerarUnoAsync(ServiciosPrueba servicios, LineaResumenCpe linea, DateOnly? fecha = null) =>
        await GenerarUnoAsync(servicios, [linea], fecha);

    private static async Task<(IReadOnlyList<XElement> Lineas, ResultadoGeneracion Resultado)> GenerarUnoAsync(ServiciosPrueba servicios, LineaResumenCpe[] lineas, DateOnly? fecha = null)
    {
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        var resultado = Assert.Single(await Servicio(servicios).GenerarResumenAsync(emisor, Resumen(lineas) with { FechaReferencia = fecha ?? _fecha }, Ct));
        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        return (GeneracionResumenTests.Lineas(await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct)), resultado);
    }

    private async Task<ResultadoGeneracion> GenerarConErrorAsync(ResumenDiarioCpe resumen)
    {
        var resultado = Assert.Single(await Servicio(_servicios).GenerarResumenAsync(CertificadoPrueba.Emisor(_servicios.Almacen), resumen, Ct));
        Assert.False(resultado.Exito);
        Assert.Null(resultado.Rutas.Zip);
        return resultado;
    }
}
