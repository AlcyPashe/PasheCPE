using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Diagnostico;
using PasheCPE.Tiempo;

namespace PasheCPE.Firma;

/// <summary>Certificado listo para firmar, o el error que lo impide; con la advertencia de vencimiento si corresponde.</summary>
internal sealed record CertificadoObtenido(X509Certificate2? Certificado, ErrorCpe? Error, ObservacionCpe? Advertencia)
{
    public bool Exito => Error is null;
}

/// <summary>
/// Único punto que toca la API de certificados (TRD §10). Carga el .pfx de la carpeta CERT con
/// <see cref="X509CertificateLoader"/> y lo guarda en caché por ruta; un cambio de fecha del archivo o de clave lo
/// recarga. La clave solo se conserva en memoria como hash SHA-256. Nunca lanza: los problemas se devuelven como error
/// PCPE (archivo inexistente, clave incorrecta, sin clave privada RSA, fuera de vigencia a la hora de Lima).
/// </summary>
internal sealed class ProveedorCertificados(
    AlmacenCpe almacen,
    CatalogoErrores errores,
    RelojCpe reloj,
    IOptions<OpcionesPasheCpe> opciones,
    ILogger<ProveedorCertificados> registro) : IDisposable
{
    private readonly ConcurrentDictionary<string, Entrada> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Los certificados reemplazados no se liberan al instante: otra firma en curso podría estar usándolos.
    private readonly ConcurrentBag<X509Certificate2> _reemplazados = [];
    private readonly Lock _carga = new();

    public CertificadoObtenido Obtener(CertificadoCpe certificado)
    {
        ArgumentNullException.ThrowIfNull(certificado);
        string ruta;
        try
        {
            ruta = almacen.RutaCertificado(certificado.Archivo);
        }
        catch (ArgumentException)
        {
            return Fallo("PCPE-3001", certificado.Archivo);
        }

        if (!File.Exists(ruta))
        {
            return Fallo("PCPE-3001", certificado.Archivo);
        }

        var modificado = File.GetLastWriteTimeUtc(ruta);
        var hashClave = SHA256.HashData(Encoding.UTF8.GetBytes(certificado.Clave ?? string.Empty));
        if (!_cache.TryGetValue(ruta, out var entrada) || !entrada.Coincide(modificado, hashClave))
        {
            lock (_carga)
            {
                if (!_cache.TryGetValue(ruta, out entrada) || !entrada.Coincide(modificado, hashClave))
                {
                    var cargado = Cargar(ruta, certificado);
                    if (cargado.Error is not null)
                    {
                        return cargado;
                    }

                    var nueva = new Entrada(modificado, hashClave, cargado.Certificado!);
                    if (entrada is not null)
                    {
                        _reemplazados.Add(entrada.Certificado);
                    }

                    _cache[ruta] = nueva;
                    entrada = nueva;
                }
            }
        }

        return Vigencia(entrada.Certificado, certificado.Archivo);
    }

    public void Dispose()
    {
        foreach (var entrada in _cache.Values)
        {
            entrada.Certificado.Dispose();
        }

        foreach (var certificado in _reemplazados)
        {
            certificado.Dispose();
        }

        _cache.Clear();
    }

    private CertificadoObtenido Cargar(string ruta, CertificadoCpe certificado)
    {
        X509Certificate2 cargado;
        try
        {
            cargado = X509CertificateLoader.LoadPkcs12FromFile(
                ruta,
                certificado.Clave,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            return Fallo("PCPE-3002", certificado.Archivo);
        }

        using (var clavePrivada = cargado.GetRSAPrivateKey())
        {
            if (clavePrivada is null)
            {
                cargado.Dispose();
                return Fallo("PCPE-3003", certificado.Archivo);
            }
        }

        var obtenido = Vigencia(cargado, certificado.Archivo);
        if (obtenido.Advertencia is not null)
        {
            RegistroEventos.CertificadoPorVencer(registro, certificado.Archivo, Fecha(cargado.NotAfter));
        }

        return obtenido.Error is null ? obtenido : Liberar(cargado, obtenido);
    }

    private CertificadoObtenido Vigencia(X509Certificate2 certificado, string archivo)
    {
        var ahora = reloj.AhoraLima();
        var desde = Lima(certificado.NotBefore);
        var hasta = Lima(certificado.NotAfter);
        if (ahora < desde || ahora > hasta)
        {
            return new CertificadoObtenido(null, errores.Local("PCPE-3004", OrigenError.Firma, null, null, archivo, Fecha(certificado.NotBefore), Fecha(certificado.NotAfter)), null);
        }

        var dias = (int)Math.Floor((hasta - ahora).TotalDays);
        var advertencia = dias < opciones.Value.AvisoVencimientoCertificadoDias
            ? errores.Advertencia("PCPE-3005", archivo, Fecha(certificado.NotAfter), dias)
            : null;
        return new CertificadoObtenido(certificado, null, advertencia);
    }

    private static CertificadoObtenido Liberar(X509Certificate2 certificado, CertificadoObtenido obtenido)
    {
        certificado.Dispose();
        return obtenido;
    }

    private CertificadoObtenido Fallo(string codigo, string archivo) =>
        new(null, errores.Local(codigo, OrigenError.Firma, null, null, archivo), null);

    /// <summary>NotBefore y NotAfter vienen en hora local del servidor; se pasan a Lima.</summary>
    private static DateTimeOffset Lima(DateTime local) => TimeZoneInfo.ConvertTime(new DateTimeOffset(local), ZonaLima.Zona);

    private static string Fecha(DateTime local) => Lima(local).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private sealed record Entrada(DateTime Modificado, byte[] HashClave, X509Certificate2 Certificado)
    {
        public bool Coincide(DateTime modificado, byte[] hashClave) =>
            Modificado == modificado && CryptographicOperations.FixedTimeEquals(HashClave, hashClave);
    }
}
