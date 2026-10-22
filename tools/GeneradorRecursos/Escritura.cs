using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace PasheCPE.GeneradorRecursos;

/// <summary>
/// Escritura determinista de la salida: UTF-8 sin BOM, saltos de línea <c>\n</c>, orden fijo y sin marcas de
/// tiempo, para que dos ejecuciones produzcan los mismos bytes y el diff muestre solo cambios reales de SUNAT.
/// </summary>
internal static partial class Escritura
{
    private static readonly JsonWriterOptions _opcionesJson = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Json(Action<Utf8JsonWriter> escribir)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var escritor = new Utf8JsonWriter(buffer, _opcionesJson))
        {
            escribir(escritor);
        }

        return [.. buffer.WrittenSpan, (byte)'\n'];
    }

    public static byte[] Utf8(string texto) => new UTF8Encoding(false).GetBytes(texto);

    public static void Fuente(Utf8JsonWriter json, Fuente fuente)
    {
        json.WriteStartObject("fuente");
        json.WriteString("archivo", fuente.Archivo);
        json.WriteString("sha256", fuente.Sha256);
        json.WriteEndObject();
    }

    public static void Fecha(Utf8JsonWriter json, string nombre, DateOnly? fecha)
    {
        if (fecha is { } valor)
        {
            json.WriteString(nombre, valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }

    public static void TextoOpcional(Utf8JsonWriter json, string nombre, string? valor)
    {
        if (!string.IsNullOrEmpty(valor))
        {
            json.WriteString(nombre, valor);
        }
    }

    public static byte[] CatalogoXml(Catalogo catalogo)
    {
        using var memoria = new MemoryStream();
        var opciones = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
        };
        using (var xml = XmlWriter.Create(memoria, opciones))
        {
            xml.WriteStartDocument();
            xml.WriteStartElement("l");
            xml.WriteAttributeString("id", catalogo.Numero);
            xml.WriteAttributeString("nombre", catalogo.Nombre);
            if (catalogo.Formato is not null)
            {
                xml.WriteAttributeString("soloFormato", "true");
                xml.WriteAttributeString("formato", catalogo.Formato);
            }

            AtributoFecha(xml, "desde", catalogo.Desde);
            AtributoFecha(xml, "hasta", catalogo.Hasta);
            foreach (var item in catalogo.Items)
            {
                xml.WriteStartElement("c");
                xml.WriteAttributeString("id", item.Codigo);
                AtributoFecha(xml, "desde", item.Desde);
                AtributoFecha(xml, "hasta", item.Hasta);
                foreach (var (columna, valor) in item.Extras)
                {
                    xml.WriteAttributeString(NombreAtributo(columna), valor);
                }

                xml.WriteString(item.Descripcion);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return [.. memoria.ToArray(), (byte)'\n'];
    }

    /// <summary>Nombre de atributo XML a partir del encabezado de una columna («Código internacional» → codigo-internacional).</summary>
    public static string NombreAtributo(string encabezado)
    {
        var nombre = NoAlfanumerico().Replace(Texto.SinTildes(encabezado).ToLowerInvariant(), "-").Trim('-');
        nombre = nombre is "id" or "desde" or "hasta" or "" ? "col-" + nombre : nombre;
        return char.IsDigit(nombre[0]) ? "c-" + nombre : nombre;
    }

    private static void AtributoFecha(XmlWriter xml, string nombre, DateOnly? fecha)
    {
        if (fecha is { } valor)
        {
            xml.WriteAttributeString(nombre, valor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NoAlfanumerico();
}

/// <summary>Archivo del que salen los recursos y su huella, para saber de qué versión vienen.</summary>
internal sealed record Fuente(string Archivo, string Sha256);
