using Microsoft.Extensions.DependencyInjection;
using PasheCPE.Pdf.Disenos;
using PasheCPE.Pdf.Representacion;
using PasheCPE.Pdf.Tests.Apoyo;
using PasheCPE.Tests.Utilidades;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PasheCPE.Pdf.Tests;

public sealed class QrPdfTests : IDisposable
{
    private readonly ServiciosPrueba _servicios;

    public QrPdfTests()
    {
        _servicios = new ServiciosPrueba(registrar: s => s.AddPasheCpePdf());
    }

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ContenidoQr_DesdeElXmlFirmado_IgualAlDeLaGeneracion(bool cerosNumero, bool separadorFinal)
    {
        using var servicios = new ServiciosPrueba(o => o.Qr = new OpcionesQr { CerosNumero = cerosNumero, SeparadorFinal = separadorFinal }, registrar: s => s.AddPasheCpePdf());
        var emisor = CertificadoPrueba.Emisor(servicios.Almacen);
        foreach (var cpe in new[] { Ejemplos.Factura(), CasosTributarios.Todos["BoletaClienteIdentificado"].Comprobante, CasosTributarios.Todos["Icbper"].Comprobante })
        {
            var generacion = await servicios.Obtener<IServicioCpe>().GenerarAsync(emisor, cpe, Ct);
            Assert.True(generacion.Exito);

            var lectura = await servicios.Obtener<LectorRepresentacion>().LeerAsync(generacion.Referencia, Ct);

            Assert.Equal(generacion.ContenidoQr, lectura.Comprobante!.ContenidoQr);
            Assert.Equal(generacion.ValorResumen, lectura.Comprobante.ValorResumen);
        }
    }

    [Fact]
    public void BloqueQr_SeImprimeDe35x35Mm()
    {
        const string contenido = "20493400364|01|F001|2|0.00|129.80|2026-10-23|6|20611619767|abc123=";
        const int dpi = 300;
        var documento = Document.Create(c => c.Page(p =>
        {
            p.Size(120, 60, Unit.Millimetre);
            p.Margin(0);
            p.Content().Element(x => ComponentesComunes.BloqueQr(x, contenido, null, " ", null));
        }));

        var imagen = ImagenPng.Leer(documento.GenerateImages(new ImageGenerationSettings { RasterDpi = dpi, ImageFormat = ImageFormat.Png }).First());
        var lado = ComponentesComunes.LadoQrMm / 25.4 * dpi;
        var zona = imagen.ZonaOscura(0, 0, (int)Math.Ceiling(lado) + 2, imagen.Alto);

        // La imagen de 35 mm incluye la zona de silencio de 4 módulos: lo oscuro ocupa los módulos sin ella.
        using var datos = QRCodeGenerator.GenerateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);
        var modulos = datos.ModuleMatrix.Count;
        var esperado = lado * (modulos - 8) / modulos;
        var silencio = lado * 4 / modulos;
        Assert.InRange(zona.Ancho, esperado - 3, esperado + 3);
        Assert.InRange(zona.Alto, esperado - 3, esperado + 3);
        Assert.InRange(zona.X, silencio - 3, silencio + 3);
        Assert.InRange(silencio / dpi * 25.4, 1.0, double.MaxValue); // zona de silencio ≥ 1 mm (Contrato Bottega)
    }

    [Fact]
    public async Task DisenoComprobante_ConMuchasLineas_VariasPaginas()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.NotaDebito);
        var cpe = (await _servicios.Obtener<LectorRepresentacion>().LeerAsync(referencia, Ct)).Comprobante!;
        var linea = cpe.Lineas[0];
        var largo = cpe with { Lineas = [.. Enumerable.Range(1, 300).Select(i => linea with { Numero = i })] };
        var estilo = new EstiloPdf(MarcaEmisor.SinMarca, null, QuestPDF.Helpers.Colors.Blue.Darken3);

        var paginas = new DisenoComprobante(largo, estilo).GenerateImages(new ImageGenerationSettings { RasterDpi = 20 }).Count();

        Assert.True(paginas > 1, $"Páginas: {paginas}");
    }
}
