using System.Globalization;
using Microsoft.Extensions.Options;

namespace PasheCPE.Almacenamiento;

/// <summary>Carpetas del almacén con periodo yyyy\MM (la carpeta CERT no tiene periodo).</summary>
internal enum CarpetaCpe
{
    Data,
    Firma,
    Envio,
    Rpta,
    Repo,
    Qr,
    Log,
}

/// <summary>
/// Único componente que compone rutas del almacén (PRD RF-20, TRD §18): <c>{RutaBase}\{CARPETA}\yyyy\MM\{archivo}</c>
/// y <c>{RutaBase}\CERT\{archivo}</c>. Nunca crea subcarpetas por día. Las escrituras son atómicas.
/// </summary>
internal sealed class AlmacenCpe(IOptions<OpcionesPasheCpe> opciones)
{
    public string RutaBase => opciones.Value.RutaBase;

    /// <summary>Ruta de un certificado en CERT (sin subcarpetas).</summary>
    public string RutaCertificado(string archivo) => Path.Combine(RutaBase, "CERT", ValidarNombre(archivo));

    /// <summary>Ruta de un archivo en la carpeta y el periodo (año y mes) de la fecha.</summary>
    public string Ruta(CarpetaCpe carpeta, DateOnly fecha, string archivo) =>
        Path.Combine(
            RutaBase,
            Nombre(carpeta),
            fecha.Year.ToString("0000", CultureInfo.InvariantCulture),
            fecha.Month.ToString("00", CultureInfo.InvariantCulture),
            ValidarNombre(archivo));

    /// <summary>Ruta de un archivo del documento en el periodo de su fecha (emisión o generación).</summary>
    public string Ruta(CarpetaCpe carpeta, ReferenciaCpe referencia, string archivo)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        return Ruta(carpeta, referencia.Fecha, archivo);
    }

    /// <summary>Carpeta de un periodo (para listar el historial del mes).</summary>
    public string CarpetaPeriodo(CarpetaCpe carpeta, int anio, int mes) =>
        Path.GetDirectoryName(Ruta(carpeta, new DateOnly(anio, mes, 1), "x"))!;

    /// <summary>Escribe el archivo de forma atómica: temporal en la misma carpeta y luego reemplazo.</summary>
    public static async Task EscribirAsync(string ruta, ReadOnlyMemory<byte> contenido, CancellationToken ct)
    {
        var carpeta = Path.GetDirectoryName(ruta) ?? throw new ArgumentException("La ruta no tiene carpeta.", nameof(ruta));
        Directory.CreateDirectory(carpeta);
        var temporal = Path.Combine(carpeta, $".{Path.GetFileName(ruta)}.{Guid.NewGuid():N}.tmp");
        try
        {
            var flujo = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await using (flujo.ConfigureAwait(false))
            {
                await flujo.WriteAsync(contenido, ct).ConfigureAwait(false);
                await flujo.FlushAsync(ct).ConfigureAwait(false);
            }

            File.Move(temporal, ruta, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporal))
            {
                File.Delete(temporal);
            }
        }
    }

    /// <summary>Contenido del archivo, o null si no existe.</summary>
    public static async Task<byte[]?> LeerAsync(string ruta, CancellationToken ct) =>
        File.Exists(ruta) ? await File.ReadAllBytesAsync(ruta, ct).ConfigureAwait(false) : null;

    private static string Nombre(CarpetaCpe carpeta) => carpeta switch
    {
        CarpetaCpe.Data => "DATA",
        CarpetaCpe.Firma => "FIRMA",
        CarpetaCpe.Envio => "ENVIO",
        CarpetaCpe.Rpta => "RPTA",
        CarpetaCpe.Repo => "REPO",
        CarpetaCpe.Qr => "QR",
        CarpetaCpe.Log => "LOG",
        _ => throw new ArgumentOutOfRangeException(nameof(carpeta), carpeta, null),
    };

    /// <summary>Solo nombres de archivo: impide escribir fuera del almacén con rutas o «..».</summary>
    private static string ValidarNombre(string archivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivo);
        if (archivo != Path.GetFileName(archivo) || archivo is "." or ".." || archivo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"'{archivo}' no es un nombre de archivo válido para el almacén.", nameof(archivo));
        }

        return archivo;
    }
}
