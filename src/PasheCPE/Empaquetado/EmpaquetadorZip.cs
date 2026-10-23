using System.IO.Compression;

namespace PasheCPE.Empaquetado;

/// <summary>
/// ZIP de envío (TRD §11): una sola entrada en la raíz, <c>{nombre}.xml</c>, con el XML firmado tal cual. La fecha de
/// la entrada es la del proceso, así el ZIP no depende de la hora del servidor.
/// </summary>
internal static class EmpaquetadorZip
{
    public static byte[] Empaquetar(string nombreEntrada, ReadOnlySpan<byte> contenido, DateTimeOffset fecha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreEntrada);
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entrada = zip.CreateEntry(nombreEntrada, CompressionLevel.Optimal);
            entrada.LastWriteTime = fecha;
            using var flujo = entrada.Open();
            flujo.Write(contenido);
        }

        return memoria.ToArray();
    }
}
