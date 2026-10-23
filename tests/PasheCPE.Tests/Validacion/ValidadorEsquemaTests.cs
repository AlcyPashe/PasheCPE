using System.Text;
using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Validacion;

namespace PasheCPE.Tests.Validacion;

public sealed class ValidadorEsquemaTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    [Fact]
    public async Task Validar_XmlGenerado_EsValido()
    {
        var xml = await Generar();

        Assert.Empty(_servicios.Obtener<ValidadorEsquema>().Validar(xml));
    }

    [Fact]
    public async Task Validar_FaltaElementoObligatorio_DevuelveErrorConRuta()
    {
        var xml = Reemplazar(await Generar(), "<cbc:ID>F001-123</cbc:ID>", string.Empty);

        var error = Assert.Single(_servicios.Obtener<ValidadorEsquema>().Validar(xml));

        Assert.Equal("PCPE-2001", error.Codigo);
        Assert.Equal(OrigenError.Esquema, error.Origen);
        Assert.StartsWith("/Invoice", error.Ruta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validar_ElementoFueraDeOrden_DevuelveError()
    {
        var xml = Reemplazar(await Generar(), "<cbc:UBLVersionID>2.1</cbc:UBLVersionID>", string.Empty);
        xml = Reemplazar(xml, "</cac:InvoiceLine></Invoice>", "</cac:InvoiceLine><cbc:UBLVersionID>2.1</cbc:UBLVersionID></Invoice>");

        Assert.NotEmpty(_servicios.Obtener<ValidadorEsquema>().Validar(xml));
    }

    [Fact]
    public void Validar_XmlMalFormado_DevuelveErrorSinLanzar()
    {
        var error = Assert.Single(_servicios.Obtener<ValidadorEsquema>().Validar(Encoding.UTF8.GetBytes("<Invoice><sin-cerrar></Invoice>")));

        Assert.Equal("PCPE-2001", error.Codigo);
    }

    [Fact]
    public async Task Validar_MontoConFormatoInvalido_DevuelveError()
    {
        var texto = Encoding.UTF8.GetString(await Generar());
        var xml = Encoding.UTF8.GetBytes(System.Text.RegularExpressions.Regex.Replace(texto, @"<cbc:PayableAmount currencyID=""PEN"">[^<]+<", @"<cbc:PayableAmount currencyID=""PEN"">abc<"));

        Assert.NotEmpty(_servicios.Obtener<ValidadorEsquema>().Validar(xml));
    }

    [Theory]
    [InlineData("20611619767-RC-20261021-11.xml")]
    [InlineData("20611619767-RC-20261022-7.xml")]
    public void Validar_ResumenDorado_PasaElXsdUbl20(string archivo)
    {
        var xml = File.ReadAllBytes(Path.Combine(Dorados.LectorDorado.Carpeta("RC-Resumen"), archivo));

        Assert.Empty(_servicios.Obtener<ValidadorEsquema>().Validar(xml));
        Assert.False(_servicios.Obtener<ValidadorEsquema>().Compilado);
    }

    [Fact]
    public void Validar_ResumenSinLineas_DevuelveErrorDelXsdUbl20()
    {
        var texto = File.ReadAllText(Path.Combine(Dorados.LectorDorado.Carpeta("RC-Resumen"), "20611619767-RC-20261021-11.xml"));
        var sinLineas = System.Text.RegularExpressions.Regex.Replace(texto, "<sac:SummaryDocumentsLine>.*</sac:SummaryDocumentsLine>", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);

        var error = Assert.Single(_servicios.Obtener<ValidadorEsquema>().Validar(Encoding.UTF8.GetBytes(sinLineas)));

        Assert.StartsWith("/SummaryDocuments", error.Ruta, StringComparison.Ordinal);
    }

    private async Task<byte[]> Generar()
    {
        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);
        Assert.True(resultado.Exito);
        return resultado.Xml!;
    }

    private static byte[] Reemplazar(byte[] xml, string buscar, string reemplazo)
    {
        var texto = Encoding.UTF8.GetString(xml);
        Assert.Contains(buscar, texto, StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(texto.Replace(buscar, reemplazo, StringComparison.Ordinal));
    }
}
