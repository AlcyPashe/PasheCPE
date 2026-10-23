using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PasheCPE.Mapeo;
using PasheCPE.Serializacion;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Ubl;

namespace PasheCPE.Tests.Serializacion;

public sealed partial class SerializadorUblTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    [Theory]
    [InlineData("es-PE")]
    [InlineData("es-ES")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public async Task Serializar_CualquierCultura_UsaPuntoDecimal(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);
            var texto = Encoding.UTF8.GetString(await Generar());

            Assert.Contains("<cbc:PayableAmount currencyID=\"PEN\">61.00</cbc:PayableAmount>", texto, StringComparison.Ordinal);
            Assert.Contains("<cbc:Percent>18.00</cbc:Percent>", texto, StringComparison.Ordinal);
            Assert.DoesNotContain("61,00", texto, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public async Task Serializar_PrefijosYSinElementosVacios()
    {
        var texto = Encoding.UTF8.GetString(await Generar());
        var raiz = texto[..texto.IndexOf('>', texto.IndexOf("<Invoice", StringComparison.Ordinal))];

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", texto, StringComparison.Ordinal);
        Assert.Equal(
            ["xmlns:cac", "xmlns:cbc", "xmlns:ext", "xmlns"],
            EspacioDeNombres().Matches(raiz).Select(m => m.Groups[1].Value));
        var vacios = ElementoVacio().Matches(texto).Select(m => m.Value).Where(v => !v.StartsWith("<ext:ExtensionContent", StringComparison.Ordinal));
        Assert.Empty(vacios);
        Assert.DoesNotContain('\n', texto);
    }

    [Fact]
    public async Task Deserializar_IdaYVuelta_ProduceLosMismosBytes()
    {
        var xml = await Generar();

        var invoice = SerializadorUbl.Deserializar<Invoice>(xml);

        Assert.Equal("F001-123", invoice.ID?.Value);
        Assert.Equal(61.00m, invoice.LegalMonetaryTotal?.PayableAmount?.Valor);
        Assert.Equal(xml, SerializadorUbl.Serializar(invoice));
    }

    [Fact]
    public async Task Serializar_UsaElSerializadorPregenerado()
    {
        await Generar();

        Assert.Contains(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "PasheCPE.XmlSerializers");
    }

    [Fact]
    public void Serializar_PrecioUnitario_HastaDiezDecimales()
    {
        var precio = new AmountType { CurrencyId = "PEN", Valor = 0.8474576271m, Formato = FormatoUbl.Unitario };
        var monto = new AmountType { CurrencyId = "PEN", Valor = 2.5m };

        Assert.Equal(("0.8474576271", "2.50"), (precio.Texto, monto.Texto));
    }

    private async Task<byte[]> Generar()
    {
        var resultado = await _servicios.Obtener<GeneradorInvoice>().GenerarAsync(Ejemplos.Emisor(), Ejemplos.Factura(), TestContext.Current.CancellationToken);
        Assert.True(resultado.Exito);
        return resultado.Xml!;
    }

    [GeneratedRegex(@"(xmlns(?::\w+)?)=")]
    private static partial Regex EspacioDeNombres();

    [GeneratedRegex(@"<[A-Za-z]+:[A-Za-z]+(?:\s[^>]*)?/>")]
    private static partial Regex ElementoVacio();
}
