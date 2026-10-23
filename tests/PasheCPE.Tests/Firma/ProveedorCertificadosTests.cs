using System.Security.Cryptography.X509Certificates;
using PasheCPE.Firma;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Firma;

public sealed class ProveedorCertificadosTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private ProveedorCertificados Proveedor => _servicios.Obtener<ProveedorCertificados>();

    [Fact]
    public void Obtener_CertificadoVigente_DevuelveClavePrivadaSinAdvertencia()
    {
        var certificado = CertificadoPrueba.Crear(_servicios.Almacen);

        var obtenido = Proveedor.Obtener(certificado);

        Assert.True(obtenido.Exito);
        Assert.Null(obtenido.Advertencia);
        using var clave = obtenido.Certificado!.GetRSAPrivateKey();
        Assert.NotNull(clave);
    }

    [Fact]
    public void Obtener_DosVeces_ReutilizaLaCache()
    {
        var certificado = CertificadoPrueba.Crear(_servicios.Almacen);

        var primero = Proveedor.Obtener(certificado);
        var segundo = Proveedor.Obtener(certificado);

        Assert.Same(primero.Certificado, segundo.Certificado);
    }

    [Fact]
    public void Obtener_ArchivoReemplazado_Recarga()
    {
        var primero = Proveedor.Obtener(CertificadoPrueba.Crear(_servicios.Almacen));
        var nuevo = CertificadoPrueba.Crear(_servicios.Almacen);
        File.SetLastWriteTimeUtc(Path.Combine(_servicios.Almacen.Ruta, "CERT", nuevo.Archivo), DateTime.UtcNow.AddMinutes(1));

        var segundo = Proveedor.Obtener(nuevo);

        Assert.True(segundo.Exito);
        Assert.NotEqual(primero.Certificado!.Thumbprint, segundo.Certificado!.Thumbprint);
    }

    [Fact]
    public void Obtener_ClaveIncorrecta_DevuelveError3002()
    {
        var certificado = CertificadoPrueba.Crear(_servicios.Almacen) with { Clave = "otra-clave" };

        var obtenido = Proveedor.Obtener(certificado);

        Assert.Equal(("PCPE-3002", OrigenError.Firma), (obtenido.Error!.Codigo, obtenido.Error.Origen));
        Assert.Null(obtenido.Certificado);
    }

    [Fact]
    public void Obtener_ClaveIncorrectaTrasUnaCorrecta_NoUsaLaCache()
    {
        var certificado = CertificadoPrueba.Crear(_servicios.Almacen);
        Assert.True(Proveedor.Obtener(certificado).Exito);

        var obtenido = Proveedor.Obtener(certificado with { Clave = "otra-clave" });

        Assert.Equal("PCPE-3002", obtenido.Error!.Codigo);
    }

    [Theory]
    [InlineData("no-existe.pfx")]
    [InlineData("..\\fuera.pfx")]
    public void Obtener_ArchivoInexistenteOFueraDeCert_DevuelveError3001(string archivo)
    {
        var obtenido = Proveedor.Obtener(new CertificadoCpe(archivo, "clave"));

        Assert.Equal("PCPE-3001", obtenido.Error!.Codigo);
    }

    [Fact]
    public void Obtener_SinClavePrivada_DevuelveError3003()
    {
        var certificado = CertificadoPrueba.Crear(_servicios.Almacen, conClavePrivada: false);

        var obtenido = Proveedor.Obtener(certificado);

        Assert.Equal("PCPE-3003", obtenido.Error!.Codigo);
    }

    [Fact]
    public void Obtener_Vencido_DevuelveError3004()
    {
        var certificado = CertificadoPrueba.Crear(_servicios.Almacen, desde: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), hasta: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var obtenido = Proveedor.Obtener(certificado);

        Assert.Equal("PCPE-3004", obtenido.Error!.Codigo);
    }

    [Fact]
    public void Obtener_VenceEnMenosDe30Dias_Advierte3005()
    {
        // El reloj de prueba marca el 22/10/2026 a las 10:00 de Lima; el certificado vence 18 días después.
        var certificado = CertificadoPrueba.Crear(_servicios.Almacen, hasta: new DateTimeOffset(2026, 11, 9, 15, 0, 0, TimeSpan.Zero));

        var obtenido = Proveedor.Obtener(certificado);

        Assert.True(obtenido.Exito);
        Assert.Equal("PCPE-3005", obtenido.Advertencia!.Codigo);
        Assert.Contains("18 días", obtenido.Advertencia.Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public void Obtener_AvisoConfigurable_SinAdvertenciaConMenosDias()
    {
        using var servicios = new ServiciosPrueba(o => o.AvisoVencimientoCertificadoDias = 10);
        var certificado = CertificadoPrueba.Crear(servicios.Almacen, hasta: new DateTimeOffset(2026, 11, 9, 15, 0, 0, TimeSpan.Zero));

        var obtenido = servicios.Obtener<ProveedorCertificados>().Obtener(certificado);

        Assert.Null(obtenido.Advertencia);
    }
}
