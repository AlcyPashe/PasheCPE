using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;
using PasheCPE.Validacion.Reglas;

namespace PasheCPE.Tests.Mapeo;

public sealed class GeneradorInvoiceTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    [Fact]
    public async Task GenerarAsync_FacturaValida_EscribeEnDataDelPeriodo()
    {
        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito, string.Join(Environment.NewLine, resultado.Errores.Select(e => $"{e.Codigo} {e.Ruta}: {e.Descripcion}")));
        Assert.Equal(Path.Combine(_servicios.Almacen.Ruta, "DATA", "2026", "10", "20123456786-01-F001-123.xml"), resultado.Ruta);
        Assert.Equal(resultado.Xml, await File.ReadAllBytesAsync(resultado.Ruta!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GenerarAsync_ConErroresDeModelo_NoEscribeNada()
    {
        var factura = Ejemplos.Factura() with { Serie = "B001" };

        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), factura, TestContext.Current.CancellationToken);

        Assert.False(resultado.Exito);
        Assert.Contains(resultado.Errores, e => e.Codigo == "PCPE-1204");
        Assert.Null(resultado.Ruta);
        Assert.False(Directory.Exists(Path.Combine(_servicios.Almacen.Ruta, "DATA")));
    }

    [Fact]
    public async Task GenerarAsync_ObservacionDeSunat_VaALasAdvertenciasYSeEscribe()
    {
        // Código de producto que termina en 0000: la XSL de SUNAT lo observa (4337) sin rechazarlo.
        var factura = Ejemplos.Factura() with { Lineas = [Ejemplos.Factura().Lineas[0] with { CodigoProductoSunat = "50220000" }], FormaPago = FormaPagoCpe.Contado };

        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), factura, TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito);
        Assert.Contains(resultado.Advertencias, a => a.Codigo == "4337");
        Assert.True(File.Exists(resultado.Ruta));
    }

    [Fact]
    public async Task GenerarAsync_ErrorDeValidacionSunat_NoEscribeNada()
    {
        using var servicios = new ServiciosPrueba(registrar: s => s.AddSingleton<IReglaCpe, ReglaQueSiempreFalla>());

        var resultado = await servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);

        var error = Assert.Single(resultado.Errores);
        Assert.Equal(("3291", OrigenError.ReglasComplementarias), (error.Codigo, error.Origen));
        Assert.NotNull(resultado.Xml);
        Assert.Null(resultado.Ruta);
        Assert.False(Directory.Exists(Path.Combine(servicios.Almacen.Ruta, "DATA")));
    }

    [Fact]
    public async Task GenerarAsync_IscMontoFijo_InformaLaTasaEquivalente()
    {
        var caso = CasosTributarios.Todos["IscMontoFijo"];

        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(caso.Emisor, caso.Comprobante, TestContext.Current.CancellationToken);

        var isc = XDocument.Parse(Encoding.UTF8.GetString(resultado.Xml!)).Root!.Element(XmlDePrueba.Cac + "InvoiceLine")!.Subtotal("2000");
        Assert.Equal("3", isc.Element(XmlDePrueba.Cac + "TaxCategory")!.Element(XmlDePrueba.Cbc + "Percent")!.Value);
    }

    [Fact]
    public async Task GenerarAsync_Detraccion_SchemeNameDelExcel()
    {
        var caso = CasosTributarios.Todos["Detraccion"];

        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(caso.Emisor, caso.Comprobante, TestContext.Current.CancellationToken);

        Assert.Contains("schemeName=\"Codigo de detraccion\"", Encoding.UTF8.GetString(resultado.Xml!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerarAsync_ValidacionDeModeloDesactivada_NoAplicaLasReglasDelModelo()
    {
        // Factura a un cliente con DNI: el modelo la rechaza (PCPE-1210). Sin esa capa, el error lo detecta la XSL de SUNAT.
        using var servicios = new ServiciosPrueba(o => o.Validacion.Modelo = false);
        var factura = Ejemplos.Factura() with { Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "CLIENTE" } };

        var conModelo = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), factura, TestContext.Current.CancellationToken);
        var sinModelo = await servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), factura, TestContext.Current.CancellationToken);

        Assert.Contains(conModelo.Errores, e => e.Codigo == "PCPE-1210");
        Assert.DoesNotContain(sinModelo.Errores, e => e.Origen == OrigenError.Modelo);
        Assert.NotNull(sinModelo.Xml);
    }

    [Fact]
    public async Task GenerarAsync_ValidacionDeEsquemaDesactivada_NoCompilaNiValidaLosXsd()
    {
        using var servicios = new ServiciosPrueba(o => o.Validacion.Esquema = false);

        var resultado = await servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito);
        Assert.False(servicios.Obtener<ValidadorEsquema>().Compilado);

        // Con la opción activa (por defecto), la misma generación sí compila y aplica los XSD.
        await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);
        Assert.True(_servicios.Obtener<ValidadorEsquema>().Compilado);
    }

    [Fact]
    public async Task GenerarAsync_SinValidacionLocal_GeneraLoQueSunatRechazara()
    {
        using var servicios = new ServiciosPrueba(o =>
        {
            o.Validacion.Modelo = false;
            o.Validacion.Esquema = false;
            o.Validacion.XslSunat = false;
            o.Validacion.ReglasComplementarias = false;
        });
        var factura = Ejemplos.Factura() with { Cliente = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Dni, NumeroDocumento = "12345678", RazonSocial = "CLIENTE" } };

        var resultado = await servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), factura, TestContext.Current.CancellationToken);

        Assert.True(resultado.Exito);
        Assert.True(File.Exists(resultado.Ruta));
    }

    /// <summary>Regla que siempre informa la fila 351 de la factura (3291), para probar que un error impide escribir.</summary>
    private sealed class ReglaQueSiempreFalla : IReglaCpe
    {
        public string Nombre => "Prueba";

        public IReadOnlyList<FilaExcel> Filas { get; } = [new("01", 351)];

        public IEnumerable<HallazgoRegla> Evaluar(DocumentoXml documento) => [new HallazgoRegla(351, null, "Prueba")];
    }

    [Fact]
    public async Task GenerarAsync_Xml_EsUtf8SinBomYSinXsi()
    {
        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);
        var texto = Encoding.UTF8.GetString(resultado.Xml!);

        Assert.NotEqual(0xEF, resultado.Xml![0]);
        Assert.DoesNotContain("xmlns:xsi", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("xmlns:xsd", texto, StringComparison.Ordinal);
        Assert.Contains("<ext:UBLExtensions><ext:UBLExtension><ext:ExtensionContent /></ext:UBLExtension></ext:UBLExtensions>", texto, StringComparison.Ordinal);
    }
}
