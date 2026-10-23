using System.Buffers.Binary;
using PasheCPE.Qr;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Tributos;

namespace PasheCPE.Tests.Qr;

public sealed class QrTests : IDisposable
{
    private const string _valorResumen = "A3DcLPVh/nycPAT5+FW9wIj7ADois5KS4nIp4ZTw4Hk=";

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    [Fact]
    public void Construir_Factura_DiezCamposEnOrden()
    {
        // Factura de ejemplo (Amazonía): arroz exonerado por 51.00 y envío gravado por 10.00 (IGV 1.53).
        var contenido = Construir(_servicios);

        Assert.Equal($"20123456786|01|F001|123|1.53|61.00|2026-10-22|6|20987654326|{_valorResumen}", contenido);
        Assert.Equal(10, contenido.Split('|').Length);
    }

    [Fact]
    public void Construir_SinIgv_InformaCero()
    {
        var factura = Ejemplos.Factura() with { Lineas = [Ejemplos.Factura().Lineas[0]], FormaPago = FormaPagoCpe.Contado };

        var contenido = Construir(_servicios, factura);

        Assert.Equal("0.00", contenido.Split('|')[4]);
        Assert.Equal("51.00", contenido.Split('|')[5]);
    }

    [Fact]
    public void Construir_ConCerosYSeparadorFinal_SegunOpciones()
    {
        using var servicios = new ServiciosPrueba(o =>
        {
            o.Qr.CerosNumero = true;
            o.Qr.SeparadorFinal = true;
        });

        var contenido = Construir(servicios);

        Assert.StartsWith("20123456786|01|F001|00000123|", contenido, StringComparison.Ordinal);
        Assert.EndsWith(_valorResumen + "|", contenido, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerarPng_20PxPorModuloYZonaDeSilencioDe4Modulos()
    {
        var png = GeneradorQrPng.Generar(Construir(_servicios));

        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        var ancho = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var alto = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        Assert.Equal(ancho, alto);
        Assert.Equal(0, ancho % GeneradorQrPng.PixelesPorModulo);

        // Lado en módulos = 21 + 4 × (versión − 1) del símbolo, más 4 módulos de silencio por lado.
        var modulos = (ancho / GeneradorQrPng.PixelesPorModulo) - 8;
        Assert.True(modulos >= 21 && (modulos - 21) % 4 == 0, $"{modulos} módulos no es un tamaño de QR válido.");
        using var datos = QRCoder.QRCodeGenerator.GenerateQrCode(Construir(_servicios), QRCoder.QRCodeGenerator.ECCLevel.Q);
        Assert.Equal(datos.ModuleMatrix.Count * GeneradorQrPng.PixelesPorModulo, ancho);
    }

    private static string Construir(ServiciosPrueba servicios, ComprobanteCpe? factura = null)
    {
        var calculo = servicios.Obtener<CalculadoraTributaria>().Calcular(Ejemplos.Emisor(), factura ?? Ejemplos.Factura());
        return servicios.Obtener<ConstructorContenidoQr>().Construir(calculo, _valorResumen);
    }
}
