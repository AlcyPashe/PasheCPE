using System.Text;
using PasheCPE.Mapeo;
using PasheCPE.Tests.Utilidades;

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
