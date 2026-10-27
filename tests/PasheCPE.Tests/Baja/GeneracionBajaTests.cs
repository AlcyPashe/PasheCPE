using System.Text;
using System.Xml.Linq;
using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Tests.Baja;

/// <summary>Generación de la comunicación de baja (RA): XML, validación del modelo y del historial, correlativos. Sin red.</summary>
public sealed class GeneracionBajaTests : IDisposable
{
    internal static readonly DateOnly Fecha = new(2026, 10, 22);
    internal static readonly XNamespace Sac = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";
    private static readonly XNamespace _cbc = XmlDePrueba.Cbc;
    private static readonly XNamespace _cac = XmlDePrueba.Cac;

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServicioCpe Servicio => _servicios.Obtener<IServicioCpe>();

    [Fact]
    public async Task GenerarBajaAsync_FacturaAceptada_DejaLaRaFirmadaYLaAnotaEnElHistorialDeLaFactura()
    {
        var emisor = await FacturaAceptadaAsync(_servicios, 123);

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);

        Assert.True(resultado.Exito, Errores(resultado));
        Assert.Empty(resultado.Advertencias);
        Assert.Equal("20123456786-RA-20261022-1", resultado.NombreArchivo);
        Assert.All(new[] { resultado.Rutas.Xml, resultado.Rutas.XmlFirmado, resultado.Rutas.Zip, resultado.Rutas.Historial }, r => Assert.True(File.Exists(r), r));
        Assert.Equal(new DateOnly(2026, 10, 29), resultado.FechaLimiteEnvio);
        Assert.False(string.IsNullOrEmpty(resultado.ValorResumen));

        var raiz = XDocument.Parse(Encoding.UTF8.GetString(await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct))).Root!;
        Assert.Equal("urn:sunat:names:specification:ubl:peru:schema:xsd:VoidedDocuments-1", raiz.Name.NamespaceName);
        Assert.Equal(("2.0", "1.0", "RA-20261022-1"), (raiz.Element(_cbc + "UBLVersionID")!.Value, raiz.Element(_cbc + "CustomizationID")!.Value, raiz.Element(_cbc + "ID")!.Value));
        Assert.Equal(("2026-10-22", "2026-10-22"), (raiz.Element(_cbc + "ReferenceDate")!.Value, raiz.Element(_cbc + "IssueDate")!.Value));
        Assert.Equal(("20123456786", "6"), (raiz.Elemento(_cac + "AccountingSupplierParty", _cbc + "CustomerAssignedAccountID").Value, raiz.Elemento(_cac + "AccountingSupplierParty", _cbc + "AdditionalAccountID").Value));
        var linea = Assert.Single(raiz.Elements(Sac + "VoidedDocumentsLine"));
        Assert.Equal(
            ["1", "01", "F001", "123", "ERROR EN EL RUC DEL CLIENTE"],
            linea.Elements().Select(e => e.Value));

        var historial = _servicios.Obtener<IHistorialCpe>();
        var ra = await historial.ObtenerAsync(resultado.Referencia, Ct);
        Assert.Equal(EstadoCpe.Generado, ra!.EstadoActual);
        var informado = Assert.Single(ra.Documentos!);
        Assert.Equal(("20123456786-01-F001-123", EstadoItemResumen.Anulado), (informado.Referencia.NombreArchivo, informado.Estado));
        var factura = await historial.ObtenerAsync(Factura(123), Ct);
        Assert.Equal((EstadoCpe.Aceptado, "20123456786-RA-20261022-1"), (factura!.EstadoActual, factura.Baja?.NombreArchivo));
    }

    [Fact]
    public async Task GenerarBajaAsync_FacturaYNotasDeOtroSistema_NoRevisaElHistorialYNoLoCrea()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(9), Documento(10, TipoDocumentoSunat.NotaDeCredito, "FC01"), Documento(11, TipoDocumentoSunat.NotaDeDebito, "0001")), Ct);

        Assert.True(resultado.Exito, Errores(resultado));
        Assert.Null(await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(Factura(9), Ct));
    }

    [Fact]
    public async Task GenerarBajaAsync_DosBajasDelMismoDia_UsanCorrelativosConsecutivosIndependientesDelResumen()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var boleta = await Servicio.GenerarAsync(emisor, CasosTributarios.Boleta(CasosTributarios.Linea(1, 59m)) with { Numero = 1 }, Ct);
        Assert.True(boleta.Exito, Errores(boleta));
        var resumen = Assert.Single(await Servicio.GenerarResumenAsync(emisor, Fecha, [new DocumentoResumenCpe { TipoDocumento = TipoDocumentoSunat.BoletaDeVenta, Serie = "B001", Numero = 1 }], Ct));

        var bajas = await Task.WhenAll(
            Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(1)), Ct),
            Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(2)), Ct));

        Assert.Equal("20123456786-RC-20261022-1", resumen.NombreArchivo);
        Assert.All(bajas, b => Assert.True(b.Exito, Errores(b)));
        Assert.Equal(["20123456786-RA-20261022-1", "20123456786-RA-20261022-2"], bajas.Select(b => b.NombreArchivo).Order(StringComparer.Ordinal));
    }

    public static TheoryData<string, string> CasosModelo => new()
    {
        { "SinDocumentos", "PCPE-1501" },
        { "Boleta", "PCPE-1505" },
        { "NotaDeBoleta", "PCPE-1505" },
        { "GuiaDeRemision", "PCPE-1504" },
        { "SerieDeBoleta", "PCPE-1506" },
        { "SerieDeRecibo", "PCPE-1506" },
        { "NumeroCero", "PCPE-1205" },
        { "SinMotivo", "PCPE-1507" },
        { "Repetido", "PCPE-1509" },
        { "EmisionPosteriorALaGeneracion", "PCPE-1502" },
        { "GeneracionFutura", "PCPE-1503" },
        { "FueraDePlazo", "PCPE-1510" },
        { "RucInvalido", "PCPE-1201" },
    };

    [Theory]
    [MemberData(nameof(CasosModelo))]
    public async Task GenerarBajaAsync_ModeloInvalido_ErrorLocalSinEscribirArchivos(string caso, string codigo)
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        var baja = Comunicacion(Documento(1));
        (emisor, baja) = caso switch
        {
            "SinDocumentos" => (emisor, baja with { Documentos = [] }),
            "Boleta" => (emisor, Comunicacion(Documento(1, TipoDocumentoSunat.BoletaDeVenta, "B001"))),
            "NotaDeBoleta" => (emisor, Comunicacion(Documento(1, TipoDocumentoSunat.NotaDeCredito, "BC01"))),
            "GuiaDeRemision" => (emisor, Comunicacion(Documento(1, TipoDocumentoSunat.GuiaDeRemisionRemitente, "T001"))),
            "SerieDeBoleta" => (emisor, Comunicacion(Documento(1) with { Serie = "B001" })),
            "SerieDeRecibo" => (emisor, Comunicacion(Documento(1) with { Serie = "S001" })),
            "NumeroCero" => (emisor, Comunicacion(Documento(0))),
            "SinMotivo" => (emisor, Comunicacion(Documento(1) with { Motivo = " " })),
            "Repetido" => (emisor, Comunicacion(Documento(1), Documento(1))),
            "EmisionPosteriorALaGeneracion" => (emisor, baja with { FechaGeneracion = Fecha.AddDays(-1) }),
            "GeneracionFutura" => (emisor, baja with { FechaGeneracion = Fecha.AddDays(1) }),
            "FueraDePlazo" => (emisor, baja with { FechaReferencia = Fecha.AddDays(-8) }),
            "RucInvalido" => (emisor with { Ruc = "20123456789" }, baja),
            _ => throw new ArgumentOutOfRangeException(nameof(caso)),
        };

        var resultado = await Servicio.GenerarBajaAsync(emisor, baja, Ct);

        Assert.Contains(resultado.Errores, e => e.Codigo == codigo);
        Assert.Null(resultado.Rutas.Xml);
        Assert.False(Directory.Exists(Path.Combine(_servicios.Almacen.Ruta, "ENVIO")));
    }

    [Theory]
    [InlineData(-7, "F001", true)]
    [InlineData(-8, "F001", false)]
    [InlineData(-30, "0001", true)]
    public async Task GenerarBajaAsync_Plazo_SieteDiasDesdeLaEmisionSalvoSeriesNumericas(int dias, string serie, bool exito)
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(1) with { Serie = serie }) with { FechaReferencia = Fecha.AddDays(dias) }, Ct);

        Assert.Equal(exito, resultado.Exito);
        if (!exito)
        {
            Assert.Equal("PCPE-1510", Assert.Single(resultado.Errores).Codigo);
        }
    }

    [Fact]
    public async Task GenerarBajaAsync_MotivoCorto_GeneraConLaAdvertenciaYLaObservacionDeSunat()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(1) with { Motivo = "NO" }), Ct);

        Assert.True(resultado.Exito, Errores(resultado));
        Assert.Contains(resultado.Advertencias, a => a.Codigo == "PCPE-1508");
        Assert.Contains(resultado.Advertencias, a => a.Codigo == "4203");
    }

    [Fact]
    public async Task GenerarBajaAsync_SinValidarElModelo_LoRechazaLaXslDeSunat()
    {
        using var servicios = new ServiciosPrueba(o => o.Validacion.Modelo = false);
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);

        var resultado = await servicios.Obtener<IServicioCpe>().GenerarBajaAsync(emisor, Comunicacion(Documento(1) with { Serie = "B001" }), Ct);

        Assert.Contains(resultado.Errores, e => e.Codigo == "2310" && e.Origen == OrigenError.XslSunat);
    }

    [Fact]
    public async Task GenerarBajaAsync_FacturaGeneradaSinEnviar_ErrorPorqueSunatNoLaTiene()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        Assert.True((await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct)).Exito);

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(("PCPE-1511", "Documentos[0]"), (error.Codigo, error.Ruta));
        Assert.Null((await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(Factura(123), Ct))!.Baja);
    }

    [Fact]
    public async Task GenerarBajaAsync_FacturaRechazada_Error()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        Assert.True((await Servicio.GenerarAsync(emisor, Ejemplos.Factura(), Ct)).Exito);
        _servicios.Sunat.Responder(SendBill, ConCdr(Cdr("F001-123", "2800", "El dato ingresado en el tipo de documento de identidad del receptor no esta permitido.")));
        Assert.Equal(EstadoCpe.Rechazado, (await Servicio.EnviarAsync(emisor, Factura(123), Ct)).Estado);

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);

        Assert.Equal("PCPE-1511", Assert.Single(resultado.Errores).Codigo);
    }

    [Fact]
    public async Task GenerarBajaAsync_FacturaYaEnOtraRa_ErrorConLaRaPendiente()
    {
        var emisor = await FacturaAceptadaAsync(_servicios, 123);
        Assert.True((await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct)).Exito);

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal("PCPE-1513", error.Codigo);
        Assert.Contains("20123456786-RA-20261022-1", error.Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerarBajaAsync_FechaDistintaDeLaDelHistorial_Error()
    {
        var emisor = await FacturaAceptadaAsync(_servicios, 123, Fecha.AddDays(-1));

        var resultado = await Servicio.GenerarBajaAsync(emisor, Comunicacion(Documento(123)), Ct);

        Assert.Equal("PCPE-1514", Assert.Single(resultado.Errores).Codigo);
    }

    internal static ComunicacionBajaCpe Comunicacion(params DocumentoBajaCpe[] documentos) =>
        new() { FechaReferencia = Fecha, Documentos = documentos };

    internal static DocumentoBajaCpe Documento(long numero, string tipo = TipoDocumentoSunat.Factura, string serie = "F001") =>
        new() { TipoDocumento = tipo, Serie = serie, Numero = numero, Motivo = "ERROR EN EL RUC DEL CLIENTE" };

    internal static ReferenciaCpe Factura(long numero, DateOnly? fecha = null) =>
        ReferenciaCpe.Comprobante(Ejemplos.Ruc, TipoDocumentoSunat.Factura, "F001", numero, fecha ?? Fecha);

    internal static string Errores(ResultadoGeneracion resultado) =>
        string.Join(Environment.NewLine, resultado.Errores.Select(e => $"{e.Codigo} {e.Ruta}: {e.Descripcion}"));

    /// <summary>Genera la factura y la envía con un CDR de aceptación (SUNAT falso).</summary>
    internal static async Task<EmisorCpe> FacturaAceptadaAsync(ServiciosPrueba servicios, long numero, DateOnly? fecha = null)
    {
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        var servicio = servicios.Obtener<IServicioCpe>();
        var generada = await servicio.GenerarAsync(emisor, Ejemplos.Factura() with { Numero = numero, FechaEmision = fecha ?? Fecha }, Ct);
        Assert.True(generada.Exito, Errores(generada));
        servicios.Sunat.Responder(SendBill, ConCdr(Cdr($"F001-{numero}", "0", $"La Factura numero F001-{numero}, ha sido aceptada")));
        Assert.Equal(EstadoCpe.Aceptado, (await servicio.EnviarAsync(emisor, generada.Referencia, Ct)).Estado);
        return emisor;
    }
}
