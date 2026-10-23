using System.Text;
using System.Xml;

namespace PasheCPE.Validacion.Emulacion;

/// <summary>
/// Salida de la transformación: conserva solo los elementos <c>pcpe:resultado</c> que emiten las utilidades de
/// validación y descarta todo lo demás (las XSL de SUNAT copian el documento completo a la salida).
/// </summary>
internal sealed class RecolectorResultadosXsl : XmlWriter
{
    private readonly List<HallazgoXsl> _hallazgos = [];
    private readonly StringBuilder _valor = new();
    private Dictionary<string, string>? _atributos;
    private string? _atributo;
    private int _profundidad;
    private int _profundidadResultado;

    public IReadOnlyList<HallazgoXsl> Hallazgos => _hallazgos;

    public override WriteState WriteState => WriteState.Content;

    public override void WriteStartElement(string? prefix, string localName, string? ns)
    {
        _profundidad++;
        if (_atributos is null && string.Equals(ns, ExtensionesPasheCpe.Espacio, StringComparison.Ordinal)
            && string.Equals(localName, "resultado", StringComparison.Ordinal))
        {
            _atributos = new Dictionary<string, string>(StringComparer.Ordinal);
            _profundidadResultado = _profundidad;
        }
    }

    public override void WriteStartAttribute(string? prefix, string localName, string? ns)
    {
        if (_atributos is not null && string.IsNullOrEmpty(ns))
        {
            _atributo = localName;
            _valor.Clear();
        }
    }

    public override void WriteEndAttribute()
    {
        if (_atributo is not null && _atributos is not null)
        {
            _atributos[_atributo] = _valor.ToString();
        }

        _atributo = null;
    }

    public override void WriteEndElement() => CerrarElemento();

    public override void WriteFullEndElement() => CerrarElemento();

    public override void WriteString(string? text)
    {
        if (_atributo is not null)
        {
            _valor.Append(text);
        }
    }

    public override void WriteChars(char[] buffer, int index, int count) => WriteString(new string(buffer, index, count));

    public override void WriteCharEntity(char ch) => WriteString(ch.ToString());

    public override void WriteSurrogateCharEntity(char lowChar, char highChar) => WriteString(new string([highChar, lowChar]));

    public override void WriteWhitespace(string? ws) => WriteString(ws);

    public override void WriteRaw(char[] buffer, int index, int count) => WriteString(new string(buffer, index, count));

    public override void WriteRaw(string data) => WriteString(data);

    public override void WriteCData(string? text) => WriteString(text);

    public override void WriteEntityRef(string name)
    {
    }

    public override void WriteComment(string? text)
    {
    }

    public override void WriteProcessingInstruction(string name, string? text)
    {
    }

    public override void WriteDocType(string name, string? pubid, string? sysid, string? subset)
    {
    }

    public override void WriteBase64(byte[] buffer, int index, int count)
    {
    }

    public override void WriteStartDocument()
    {
    }

    public override void WriteStartDocument(bool standalone)
    {
    }

    public override void WriteEndDocument()
    {
    }

    public override void Flush()
    {
    }

    public override string? LookupPrefix(string ns) => null;

    private void CerrarElemento()
    {
        if (_atributos is not null && _profundidad == _profundidadResultado)
        {
            _hallazgos.Add(new HallazgoXsl(
                _atributos.GetValueOrDefault("codigo", string.Empty).Trim(),
                !string.Equals(_atributos.GetValueOrDefault("tipo"), "observacion", StringComparison.Ordinal),
                _atributos.GetValueOrDefault("ruta") is { Length: > 0 } ruta ? ruta : null,
                _atributos.GetValueOrDefault("detalle", string.Empty).Trim()));
            _atributos = null;
        }

        _profundidad--;
    }
}

/// <summary>Error u observación emitido por una XSL de SUNAT.</summary>
internal sealed record HallazgoXsl(string Codigo, bool EsError, string? Ruta, string Detalle);
