using System.Text;
using System.Xml.Linq;
using PasheCPE.Almacenamiento;
using PasheCPE.Firma;
using PasheCPE.Tests.Resumen;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Servicios;

/// <summary>Generación, validación, emisión y envío de notas de crédito y débito por <see cref="IServicioCpe"/>.</summary>
public sealed class ServicioNotasTests : IDisposable
{
    private const string SendBill = "sendBill";

    private static readonly DateOnly _fecha = new(2026, 10, 22);
    private static readonly XNamespace _cbc = XmlDePrueba.Cbc;
    private static readonly XNamespace _cac = XmlDePrueba.Cac;

    private readonly ServiciosPrueba _servicios = new();

    private EmisorCpe? _emisorAmazonia;

    public void Dispose() => _servicios.Dispose();

    /// <summary>Emisor de la Amazonía (exonerado) con un certificado autofirmado, el mismo en toda la prueba.</summary>
    private EmisorCpe EmisorAmazonia => _emisorAmazonia ??= CertificadoPrueba.Emisor(_servicios.Almacen);

    private IServicioCpe Servicio => _servicios.Obtener<IServicioCpe>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GenerarAsync_NotaDeCreditoDeFactura_DejaLosArchivosFirmadosConQrYPlazoDeTresDias()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen) with { ExoneradoAmazonia = false };

        var resultado = await Servicio.GenerarAsync(emisor, CasosNotas.Todos["NcAnulacionFactura"].Nota, Ct);

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        Assert.Equal("20123456786-07-FC01-1", resultado.NombreArchivo);
        Assert.All(new[] { resultado.Rutas.Xml, resultado.Rutas.XmlFirmado, resultado.Rutas.Zip, resultado.Rutas.Qr, resultado.Rutas.Historial }, r => Assert.True(File.Exists(r), r));
        Assert.Contains(Path.Combine("FIRMA", "2026", "10"), resultado.Rutas.XmlFirmado, StringComparison.Ordinal);
        var certificado = _servicios.Obtener<ProveedorCertificados>().Obtener(emisor.Certificado).Certificado!;
        Assert.True(FirmadorXml.Verificar(await File.ReadAllBytesAsync(resultado.Rutas.XmlFirmado!, Ct), certificado));
        Assert.StartsWith("20123456786|07|FC01|1|19.80|129.80|2026-10-22|6|20987654326|", resultado.ContenidoQr, StringComparison.Ordinal);
        Assert.Equal(new DateOnly(2026, 10, 25), resultado.FechaLimiteEnvio);

        var raiz = XDocument.Parse(Encoding.UTF8.GetString(await File.ReadAllBytesAsync(resultado.Rutas.XmlFirmado!, Ct))).Root!;
        Assert.Equal("CreditNote", raiz.Name.LocalName);
        Assert.Equal("01", raiz.Element(_cac + "DiscrepancyResponse")!.Element(_cbc + "ResponseCode")!.Value);
        var referencia = Assert.Single(raiz.Elements(_cac + "BillingReference"));
        Assert.Equal("F001-1", referencia.Element(_cac + "InvoiceDocumentReference")!.Element(_cbc + "ID")!.Value);
    }

    [Fact]
    public async Task GenerarAsync_NotaDeBoleta_PlazoDeSieteDias()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen) with { ExoneradoAmazonia = false };

        var resultado = await Servicio.GenerarAsync(emisor, CasosNotas.Todos["NcBoleta"].Nota, Ct);

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        Assert.Equal(new DateOnly(2026, 10, 29), resultado.FechaLimiteEnvio);
    }

    [Fact]
    public async Task GenerarAsync_PenalidadesSinDocumento2027_SinBillingReferenceYPlazoDeFactura()
    {
        using var servicios = new ServiciosPrueba(ahora: CasosTributarios.Ahora(new DateOnly(2027, 1, 15)));
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen) with { ExoneradoAmazonia = false };

        var resultado = await servicios.Obtener<IServicioCpe>().GenerarAsync(emisor, CasosNotas.Todos["NdPenalidadesSinDocumento2027"].Nota, Ct);

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        Assert.Equal(new DateOnly(2027, 1, 18), resultado.FechaLimiteEnvio);
        var raiz = XDocument.Parse(Encoding.UTF8.GetString(await File.ReadAllBytesAsync(resultado.Rutas.XmlFirmado!, Ct))).Root!;
        Assert.Equal("DebitNote", raiz.Name.LocalName);
        Assert.Empty(raiz.Elements(_cac + "BillingReference"));
        Assert.NotNull(raiz.Element(_cac + "RequestedMonetaryTotal"));
    }

    [Fact]
    public async Task ValidarAsync_NotaInvalida_DevuelveLosErroresSinEscribirArchivos()
    {
        var resultado = await Servicio.ValidarAsync(Ejemplos.Emisor(), CasosNotas.Todos["NcAnulacionFactura"].Nota with { DocumentoModificado = null }, Ct);

        Assert.Contains(resultado.Errores, e => e.Codigo == "PCPE-1403");
        Assert.False(Directory.Exists(Path.Combine(_servicios.Almacen.Ruta, "DATA")));
    }

    [Fact]
    public async Task EmitirAsync_NotaDeCreditoDeFactura_SeEnviaPorSendBillYQuedaAceptada()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen) with { ExoneradoAmazonia = false };
        _servicios.Sunat.Responder(SendBill, ConCdr(Cdr("FC01-1", "0", "La Nota de credito numero FC01-1, ha sido aceptada")));

        var emision = await Servicio.EmitirAsync(emisor, CasosNotas.Todos["NcAnulacionFactura"].Nota, Ct);

        Assert.True(emision.Generacion.Exito, GeneracionResumenTests.Errores(emision.Generacion));
        Assert.Equal(EstadoCpe.Aceptado, emision.Envio.Estado);
        Assert.Equal(1, _servicios.Sunat.Llamadas(SendBill));
        Assert.Equal(emision.Generacion.ContenidoQr, emision.Envio.ContenidoQr);
    }

    [Fact]
    public async Task EmitirAsync_NotaDeBoletaEnModoResumen_NoSeEnviaPorSendBill()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen) with { ExoneradoAmazonia = false };

        var emision = await Servicio.EmitirAsync(emisor, CasosNotas.Todos["NcBoleta"].Nota, Ct);

        Assert.True(emision.Generacion.Exito, GeneracionResumenTests.Errores(emision.Generacion));
        Assert.Equal("PCPE-4002", emision.Envio.Codigo);
        Assert.Equal(0, _servicios.Sunat.Llamadas(SendBill));
    }

    [Fact]
    public async Task EmitirAsync_NotaDeBoletaEnModoIndividual_SeEnviaPorSendBill()
    {
        using var servicios = new ServiciosPrueba(o => o.ModoEnvioBoletas = ModoEnvioBoletas.Individual);
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen) with { ExoneradoAmazonia = false };
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr("BC01-1", "0", "La Nota de credito numero BC01-1, ha sido aceptada")));

        var emision = await servicios.Obtener<IServicioCpe>().EmitirAsync(emisor, CasosNotas.Todos["NcBoleta"].Nota, Ct);

        Assert.Equal(EstadoCpe.Aceptado, emision.Envio.Estado);
        Assert.Equal(1, servicios.Sunat.Llamadas(SendBill));
    }

    [Fact]
    public async Task GenerarResumenAsync_DesdeFirma_InformaLaNotaDeBoletaConLaBoletaQueModifica()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen) with { ExoneradoAmazonia = false };
        var nota = await Servicio.GenerarAsync(emisor, CasosNotas.Todos["NcBoleta"].Nota, Ct);
        Assert.True(nota.Exito, GeneracionResumenTests.Errores(nota));

        var documento = new DocumentoResumenCpe { TipoDocumento = TipoDocumentoSunat.NotaDeCredito, Serie = "BC01", Numero = 1 };
        var resultado = Assert.Single(await Servicio.GenerarResumenAsync(emisor, _fecha, [documento], Ct));

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
        var linea = Assert.Single(GeneracionResumenTests.Lineas(await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct)));
        Assert.Equal("BC01-1", linea.Element(_cbc + "ID")!.Value);
        Assert.Equal("07", linea.Element(_cbc + "DocumentTypeCode")!.Value);
        var modificado = linea.Element(_cac + "BillingReference")!.Element(_cac + "InvoiceDocumentReference")!;
        Assert.Equal(("B001-1", "03"), (modificado.Element(_cbc + "ID")!.Value, modificado.Element(_cbc + "DocumentTypeCode")!.Value));
    }

    [Fact]
    public async Task GenerarAsync_OriginalFueraDelHistorial_NoValidaContraElOriginal()
    {
        var resultado = await GenerarNotaSobreFacturaAsync(totalNota: 500m);

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
    }

    [Fact]
    public async Task GenerarAsync_FacturaOriginalAceptada_NoTieneErrores()
    {
        await GenerarFacturaOriginalAsync(EstadoCpe.Aceptado);

        var resultado = await GenerarNotaSobreFacturaAsync(totalNota: 61m);

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
    }

    [Theory]
    [InlineData(EstadoCpe.Rechazado, "PCPE-1430")]
    [InlineData(EstadoCpe.Generado, "PCPE-1431")]
    [InlineData(EstadoCpe.ServicioNoDisponible, "PCPE-1431")]
    public async Task GenerarAsync_FacturaOriginalNoAceptada_DevuelveError(EstadoCpe estado, string codigo)
    {
        await GenerarFacturaOriginalAsync(estado);

        var resultado = await GenerarNotaSobreFacturaAsync(totalNota: 61m);

        Assert.Contains(resultado.Errores, e => e.Codigo == codigo);
        Assert.Null(resultado.Rutas.Xml);
    }

    [Fact]
    public async Task GenerarAsync_ImporteMayorQueElOriginal_DevuelvePcpe1433()
    {
        await GenerarFacturaOriginalAsync(EstadoCpe.Aceptado);

        var resultado = await GenerarNotaSobreFacturaAsync(totalNota: 61.01m);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("PCPE-1433", error.Codigo);
        Assert.Contains("61.01", error.Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerarAsync_OtraMonedaQueElOriginal_DevuelvePcpe1432()
    {
        await GenerarFacturaOriginalAsync(EstadoCpe.Aceptado);

        var resultado = await GenerarNotaSobreFacturaAsync(totalNota: 10m, moneda: "USD");

        Assert.Contains(resultado.Errores, e => e.Codigo == "PCPE-1432");
    }

    [Fact]
    public async Task GenerarAsync_BoletaOriginalAunNoInformada_SePermite()
    {
        // La nota de boleta puede ir en el mismo resumen que su boleta.
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen) with { ExoneradoAmazonia = false };
        var boleta = await Servicio.GenerarAsync(emisor, CasosTributarios.Boleta(CasosTributarios.Linea(1, 59m)) with { Numero = 1 }, Ct);
        Assert.True(boleta.Exito, GeneracionResumenTests.Errores(boleta));

        var resultado = await Servicio.GenerarAsync(emisor, CasosNotas.Todos["NcBoleta"].Nota, Ct);

        Assert.True(resultado.Exito, GeneracionResumenTests.Errores(resultado));
    }

    /// <summary>Factura F001-123 de 61.00 PEN (Ejemplos.Factura) generada y con el estado indicado en el historial.</summary>
    private async Task GenerarFacturaOriginalAsync(EstadoCpe estado)
    {
        var factura = await Servicio.GenerarAsync(EmisorAmazonia, Ejemplos.Factura(), Ct);
        Assert.True(factura.Exito, GeneracionResumenTests.Errores(factura));
        await _servicios.Obtener<RegistroHistorial>().RegistrarAsync(factura.Referencia, estado, null, Ct);
    }

    /// <summary>Nota de crédito por devolución total de F001-123 (con su fecha de emisión) por el total indicado.</summary>
    private Task<ResultadoGeneracion> GenerarNotaSobreFacturaAsync(decimal totalNota, string moneda = "PEN")
    {
        var nota = CasosNotas.Credito(MotivoNotaCredito.DevolucionTotal, CasosTributarios.Linea(1, totalNota)) with
        {
            Moneda = moneda,
            DocumentoModificado = new DocumentoModificadoCpe { TipoDocumento = TipoDocumentoSunat.Factura, Serie = "F001", Numero = 123, FechaEmision = _fecha },
        };
        return Servicio.GenerarAsync(EmisorAmazonia, nota, Ct);
    }
}
