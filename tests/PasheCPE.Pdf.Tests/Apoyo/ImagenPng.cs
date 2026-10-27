using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace PasheCPE.Pdf.Tests.Apoyo;

/// <summary>
/// Decodificador PNG mínimo (8 bits, gris, RGB o RGBA, sin entrelazado) para medir lo que QuestPDF dibuja en una página
/// rasterizada. Solo BCL: <see cref="ZLibStream"/> y los filtros de fila del PNG.
/// </summary>
internal sealed class ImagenPng
{
    private readonly byte[] _pixeles;
    private readonly int _canales;

    private ImagenPng(int ancho, int alto, int canales, byte[] pixeles)
    {
        Ancho = ancho;
        Alto = alto;
        _canales = canales;
        _pixeles = pixeles;
    }

    public int Ancho { get; }

    public int Alto { get; }

    /// <summary>Luminosidad 0–255 del píxel (un píxel transparente cuenta como blanco).</summary>
    public int Luz(int x, int y)
    {
        var i = ((y * Ancho) + x) * _canales;
        if (_canales is 2 or 4 && _pixeles[i + _canales - 1] < 128)
        {
            return 255;
        }

        return _canales < 3 ? _pixeles[i] : (_pixeles[i] + _pixeles[i + 1] + _pixeles[i + 2]) / 3;
    }

    /// <summary>Rectángulo que contiene todos los píxeles oscuros de la región indicada.</summary>
    public (int X, int Y, int Ancho, int Alto) ZonaOscura(int x0, int y0, int x1, int y1, int umbral = 128)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = y0; y < Math.Min(y1, Alto); y++)
        {
            for (var x = x0; x < Math.Min(x1, Ancho); x++)
            {
                if (Luz(x, y) < umbral)
                {
                    (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
                }
            }
        }

        return maxX < 0 ? default : (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public static ImagenPng Leer(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        int ancho = 0, alto = 0, canales = 0;
        using var idat = new MemoryStream();
        for (var p = 8; p < png.Length;)
        {
            var largo = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(p));
            var tipo = Encoding.ASCII.GetString(png, p + 4, 4);
            var datos = png.AsSpan(p + 8, largo);
            if (tipo == "IHDR")
            {
                ancho = BinaryPrimitives.ReadInt32BigEndian(datos);
                alto = BinaryPrimitives.ReadInt32BigEndian(datos[4..]);
                if (datos[8] != 8 || datos[12] != 0)
                {
                    throw new NotSupportedException("Solo PNG de 8 bits sin entrelazado.");
                }

                canales = datos[9] switch
                {
                    0 => 1,
                    2 => 3,
                    4 => 2,
                    6 => 4,
                    _ => throw new NotSupportedException($"Tipo de color PNG {datos[9]} no soportado."),
                };
            }
            else if (tipo == "IDAT")
            {
                idat.Write(datos);
            }

            p += 12 + largo;
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var crudo = new MemoryStream();
        zlib.CopyTo(crudo);
        return new ImagenPng(ancho, alto, canales, Desfiltrar(crudo.ToArray(), ancho, alto, canales));
    }

    private static byte[] Desfiltrar(byte[] crudo, int ancho, int alto, int canales)
    {
        var fila = ancho * canales;
        var pixeles = new byte[fila * alto];
        for (var y = 0; y < alto; y++)
        {
            var filtro = crudo[y * (fila + 1)];
            var origen = (y * (fila + 1)) + 1;
            var destino = y * fila;
            for (var x = 0; x < fila; x++)
            {
                int a = x >= canales ? pixeles[destino + x - canales] : 0;
                int b = y > 0 ? pixeles[destino - fila + x] : 0;
                int c = x >= canales && y > 0 ? pixeles[destino - fila + x - canales] : 0;
                var valor = crudo[origen + x];
                pixeles[destino + x] = (byte)(filtro switch
                {
                    0 => valor,
                    1 => valor + a,
                    2 => valor + b,
                    3 => valor + ((a + b) / 2),
                    4 => valor + Paeth(a, b, c),
                    _ => throw new NotSupportedException($"Filtro PNG {filtro} no válido."),
                });
            }
        }

        return pixeles;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var (pa, pb, pc) = (Math.Abs(p - a), Math.Abs(p - b), Math.Abs(p - c));
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
