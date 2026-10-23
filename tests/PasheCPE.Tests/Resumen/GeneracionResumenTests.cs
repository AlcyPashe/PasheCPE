using System.Text;
using System.Xml.Linq;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Resumen;

public sealed class GeneracionResumenTests : IDisposable
{
    private static readonly DateOnly _fecha = new(2026, 10, 22);
    private static readonly XNamespace _cbc = XmlDePrueba.Cbc;
    private static readonly XNamespace _cac = XmlDePrueba.Cac;
    private static readonly XNamespace _sac = "urn:sunat:names:specification:ubl:peru:schema:xsd:SunatAggregateComponents-1";

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IServicioCpe Servicio => _servicios.Obtener<IServicioCpe>();

    [Fact]
    public async Task GenerarResumenAsync_DesdeFirma_DejaElRcFirmadoConLasBoletas()
    {
        var emisor = CertificadoPrueba.Emisor(_servicios.Almacen);
        await GenerarBoletaAsync(emisor, 1, AfectacionIgv.GravadoOneroso);
        await GenerarBoletaAsync(emisor, 2, AfectacionIgv.ExoneradoOneroso);

        var resultados = await Servicio.GenerarResumenAsync(emisor, _fecha, [Documento(1), Documento(2)], Ct);

        var resultado = Assert.Single(resultados);
        Assert.True(resultado.Exito, Errores(resultado));
        Assert.Equal("20123456786-RC-20261022-1", resultado.NombreArchivo);
        Assert.All(new[] { resultado.Rutas.Xml, resultado.Rutas.XmlFirmado, resultado.Rutas.Zip, resultado.Rutas.Historial }, r => Assert.True(File.Exists(r), r));
        Assert.Equal(new DateOnly(2026, 10, 29), resultado.FechaLimiteEnvio);

        var lineas = Lineas(await File.ReadAllBytesAsync(resultado.Rutas.Xml!, Ct));
        Assert.Equal(["B001-1", "B001-2"], lineas.Select(l => l.Element(_cbc + "ID")!.Value));
        Assert.Equal("01", lineas[0].Element(_sac + "BillingPayment")!.Element(_cbc + "InstructionID")!.Value);
        Assert.Equal("02", lineas[1].Element(_sac + "BillingPayment")!.Element(_cbc + "InstructionID")!.Value);

        var historial = await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(resultado.Referencia, Ct);
        Assert.Equal(EstadoCpe.Generado, historial!.EstadoActual);
        Assert.Equal(["20123456786-03-B001-1", "20123456786-03-B001-2"], historial.Documentos!.Select(d => d.Referencia.NombreArchivo));
    }

    internal static DocumentoResumenCpe Documento(long numero, string estado = EstadoItemResumen.Adicionar) =>
        new() { TipoDocumento = TipoDocumentoSunat.BoletaDeVenta, Serie = "B001", Numero = numero, Estado = estado };

    internal static IReadOnlyList<XElement> Lineas(byte[] xml) =>
        [.. XDocument.Parse(Encoding.UTF8.GetString(xml)).Root!.Elements(_sac + "SummaryDocumentsLine")];

    internal static string Errores(ResultadoGeneracion resultado) =>
        string.Join(Environment.NewLine, resultado.Errores.Select(e => $"{e.Codigo} {e.Ruta}: {e.Descripcion}"));

    private async Task GenerarBoletaAsync(EmisorCpe emisor, long numero, string afectacion)
    {
        var boleta = CasosTributarios.Boleta(CasosTributarios.Linea(1, 59m) with { Afectacion = afectacion }) with { Numero = numero };
        var resultado = await Servicio.GenerarAsync(emisor, boleta, Ct);
        Assert.True(resultado.Exito, Errores(resultado));
    }
}
