using PasheCPE.Almacenamiento;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Almacenamiento;

public sealed class AlmacenCpeTests : IDisposable
{
    private readonly AlmacenTemporal _temporal = new();

    public void Dispose() => _temporal.Dispose();

    [Theory]
    [InlineData("Data", "DATA")]
    [InlineData("Firma", "FIRMA")]
    [InlineData("Envio", "ENVIO")]
    [InlineData("Rpta", "RPTA")]
    [InlineData("Repo", "REPO")]
    [InlineData("Qr", "QR")]
    [InlineData("Log", "LOG")]
    public void Ruta_UsaCarpetaAnioYMesSinDia(string carpeta, string nombre)
    {
        var ruta = Almacen().Ruta(Enum.Parse<CarpetaCpe>(carpeta), new DateOnly(2026, 3, 9), "20123456789-01-F001-1.xml");

        Assert.Equal(Path.Combine(_temporal.Ruta, nombre, "2026", "03", "20123456789-01-F001-1.xml"), ruta);
    }

    [Fact]
    public void Ruta_ConReferencia_UsaElPeriodoDeSuFecha() =>
        Assert.Equal(
            Path.Combine(_temporal.Ruta, "RPTA", "2026", "10", "R-20123456789-01-F001-123.zip"),
            Almacen().Ruta(CarpetaCpe.Rpta, Ejemplos.Referencia(), "R-20123456789-01-F001-123.zip"));

    [Fact]
    public void RutaCertificado_SinPeriodo() =>
        Assert.Equal(Path.Combine(_temporal.Ruta, "CERT", "certificado.pfx"), Almacen().RutaCertificado("certificado.pfx"));

    [Theory]
    [InlineData("..")]
    [InlineData(@"..\otro.xml")]
    [InlineData(@"sub\archivo.xml")]
    [InlineData("sub/archivo.xml")]
    [InlineData(@"C:\Windows\archivo.xml")]
    [InlineData(" ")]
    public void Ruta_NombreConCarpetas_Lanza(string archivo)
    {
        var almacen = Almacen();

        Assert.ThrowsAny<ArgumentException>(() => almacen.Ruta(CarpetaCpe.Data, new DateOnly(2026, 10, 22), archivo));
        Assert.ThrowsAny<ArgumentException>(() => almacen.RutaCertificado(archivo));
    }

    [Fact]
    public async Task EscribirAsync_CreaLaCarpetaYReemplazaSinDejarTemporales()
    {
        var ct = TestContext.Current.CancellationToken;
        var ruta = Almacen().Ruta(CarpetaCpe.Envio, new DateOnly(2026, 10, 22), "documento.zip");

        await AlmacenCpe.EscribirAsync(ruta, new byte[] { 1, 2, 3 }, ct);
        await AlmacenCpe.EscribirAsync(ruta, new byte[] { 4, 5 }, ct);

        Assert.Equal(new byte[] { 4, 5 }, await AlmacenCpe.LeerAsync(ruta, ct));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(ruta)!));
    }

    [Fact]
    public async Task LeerAsync_ArchivoInexistente_EsNull() =>
        Assert.Null(await AlmacenCpe.LeerAsync(Path.Combine(_temporal.Ruta, "no-existe.xml"), TestContext.Current.CancellationToken));

    private AlmacenCpe Almacen() => new(_temporal.OpcionesDi());
}
