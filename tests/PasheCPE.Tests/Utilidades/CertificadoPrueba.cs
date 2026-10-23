using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// Certificado RSA 2048 autofirmado, generado en la prueba y exportado a un .pfx en la carpeta CERT del almacén
/// temporal, con una clave aleatoria que solo vive en memoria. Nunca se usa un .pfx real.
/// </summary>
internal static class CertificadoPrueba
{
    /// <summary>Vigencia por defecto: cubre la fecha fija de <see cref="ServiciosPrueba"/> (22/10/2026) con holgura.</summary>
    public static readonly DateTimeOffset DesdePorDefecto = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset HastaPorDefecto = new(2028, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static CertificadoCpe Crear(AlmacenTemporal almacen, string archivo = "certificado.pfx", DateTimeOffset? desde = null, DateTimeOffset? hasta = null, bool conClavePrivada = true)
    {
        ArgumentNullException.ThrowIfNull(almacen);
        var clave = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        using var rsa = RSA.Create(2048);
        var solicitud = new CertificateRequest($"CN=PRUEBA PASHECPE {Ejemplos.Ruc}, OU={Ejemplos.Ruc}, C=PE", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificado = solicitud.CreateSelfSigned(desde ?? DesdePorDefecto, hasta ?? HastaPorDefecto);
        byte[] pfx;
        if (conClavePrivada)
        {
            pfx = certificado.Export(X509ContentType.Pfx, clave);
        }
        else
        {
            using var publico = X509CertificateLoader.LoadCertificate(certificado.RawData);
            pfx = publico.Export(X509ContentType.Pfx, clave);
        }

        var carpeta = Path.Combine(almacen.Ruta, "CERT");
        Directory.CreateDirectory(carpeta);
        File.WriteAllBytes(Path.Combine(carpeta, archivo), pfx);
        return new CertificadoCpe(archivo, clave);
    }

    /// <summary>Emisor de ejemplo con un certificado de prueba recién creado.</summary>
    public static EmisorCpe Emisor(AlmacenTemporal almacen) => Ejemplos.Emisor() with { Certificado = Crear(almacen) };
}
