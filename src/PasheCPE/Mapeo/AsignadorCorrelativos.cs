using System.Globalization;
using PasheCPE.Almacenamiento;

namespace PasheCPE.Mapeo;

/// <summary>
/// Correlativo del día para el resumen diario (RC) y la comunicación de baja (RA): el mayor <c>n</c> de los archivos
/// <c>RUC-TT-yyyyMMdd-n</c> que ya existen en ENVIO o en LOG del periodo, más 1 (SUNAT rechaza un identificador ya
/// presentado, 2223). Quien llama debe tener el bloqueo de <see cref="ClaveBloqueo"/> hasta escribir el ZIP, así dos
/// generaciones simultáneas no repiten el número.
/// </summary>
internal sealed class AsignadorCorrelativos(AlmacenCpe almacen)
{
    public static string ClaveBloqueo(string ruc, string tipo, DateOnly fecha) =>
        string.Create(CultureInfo.InvariantCulture, $"{ruc}-{tipo}-{fecha:yyyyMMdd}");

    public int Siguiente(string ruc, string tipo, DateOnly fecha)
    {
        var prefijo = ClaveBloqueo(ruc, tipo, fecha) + "-";
        var mayor = 0;
        foreach (var carpeta in new[] { CarpetaCpe.Envio, CarpetaCpe.Log }.Select(c => almacen.CarpetaPeriodo(c, fecha.Year, fecha.Month)))
        {
            if (!Directory.Exists(carpeta))
            {
                continue;
            }

            foreach (var archivo in Directory.EnumerateFiles(carpeta, prefijo + "*"))
            {
                var nombre = Path.GetFileNameWithoutExtension(archivo);
                if (int.TryParse(nombre.AsSpan(prefijo.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var correlativo))
                {
                    mayor = Math.Max(mayor, correlativo);
                }
            }
        }

        return mayor + 1;
    }
}
