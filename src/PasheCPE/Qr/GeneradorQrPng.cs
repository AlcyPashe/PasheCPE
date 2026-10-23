using QRCoder;

namespace PasheCPE.Qr;

/// <summary>
/// PNG del QR con QRCoder (TRD §12): corrección Q, 20 px por módulo y zona de silencio de 4 módulos. En el PDF A4 se
/// imprime a 3.5 × 3.5 cm.
/// </summary>
internal static class GeneradorQrPng
{
    public const int PixelesPorModulo = 20;

    public static byte[] Generar(string contenido)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contenido);
        using var datos = QRCodeGenerator.GenerateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(datos);
        return png.GetGraphic(PixelesPorModulo, drawQuietZones: true);
    }
}
