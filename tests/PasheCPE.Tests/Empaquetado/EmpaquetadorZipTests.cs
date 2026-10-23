using System.IO.Compression;
using System.Text;
using PasheCPE.Empaquetado;

namespace PasheCPE.Tests.Empaquetado;

public sealed class EmpaquetadorZipTests
{
    private static readonly DateTimeOffset _fecha = new(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Empaquetar_UnaEntradaEnLaRaizConElContenidoExacto()
    {
        var contenido = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Invoice>ñ</Invoice>");

        var zip = EmpaquetadorZip.Empaquetar("20123456786-01-F001-123.xml", contenido, _fecha);

        using var archivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var entrada = Assert.Single(archivo.Entries);
        Assert.Equal("20123456786-01-F001-123.xml", entrada.FullName);
        using var flujo = entrada.Open();
        using var copia = new MemoryStream();
        flujo.CopyTo(copia);
        Assert.Equal(contenido, copia.ToArray());
    }

    [Fact]
    public void Empaquetar_MismaEntradaYFecha_MismosBytes()
    {
        var contenido = Encoding.UTF8.GetBytes("<Invoice/>");

        Assert.Equal(
            EmpaquetadorZip.Empaquetar("a.xml", contenido, _fecha),
            EmpaquetadorZip.Empaquetar("a.xml", contenido, _fecha));
    }
}
