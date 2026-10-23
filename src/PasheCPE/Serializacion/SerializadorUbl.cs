using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using PasheCPE.Ubl;

namespace PasheCPE.Serializacion;

/// <summary>
/// Serialización del perfil UBL (TRD §8): UTF-8 sin BOM, sin sangría, prefijos <c>cac</c>, <c>cbc</c> y <c>ext</c>
/// explícitos, sin <c>xsi</c>/<c>xsd</c>. Los serializadores se pregeneran en compilación
/// (Microsoft.XmlSerializer.Generator → PasheCPE.XmlSerializers.dll) y se cachean por tipo.
/// </summary>
internal static class SerializadorUbl
{
    private static readonly ConcurrentDictionary<Type, (XmlSerializer Serializador, XmlSerializerNamespaces Espacios)> _cache = new();

    private static readonly XmlWriterSettings _escritura = new()
    {
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        Indent = false,
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.None,
    };

    private static readonly XmlReaderSettings _lectura = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreWhitespace = false,
    };

    public static byte[] Serializar<T>(T documento)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(documento);
        var (serializador, espacios) = Obtener(typeof(T));
        using var memoria = new MemoryStream();
        using (var escritor = XmlWriter.Create(memoria, _escritura))
        {
            serializador.Serialize(escritor, documento, espacios);
        }

        return memoria.ToArray();
    }

    public static T Deserializar<T>(byte[] xml)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var memoria = new MemoryStream(xml, writable: false);
        using var lector = XmlReader.Create(memoria, _lectura);
        return (T)(Obtener(typeof(T)).Serializador.Deserialize(lector)
            ?? throw new InvalidDataException($"El XML no contiene un {typeof(T).Name}."));
    }

    /// <summary>Crea y cachea el serializador del documento (precalentamiento).</summary>
    public static void Precalentar<T>()
        where T : class => Obtener(typeof(T));

    private static (XmlSerializer Serializador, XmlSerializerNamespaces Espacios) Obtener(Type tipo) =>
        _cache.GetOrAdd(tipo, static t =>
        {
            var raiz = t.GetCustomAttribute<XmlRootAttribute>()
                ?? throw new InvalidOperationException($"{t.Name} no es un documento UBL (falta [XmlRoot]).");
            var espacios = new XmlSerializerNamespaces();
            espacios.Add(string.Empty, raiz.Namespace);
            espacios.Add("cac", EspaciosUbl.Cac);
            espacios.Add("cbc", EspaciosUbl.Cbc);
            espacios.Add("ext", EspaciosUbl.Ext);
            return (new XmlSerializer(t), espacios);
        });
}
