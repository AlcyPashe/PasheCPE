using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text.RegularExpressions;
using System.Xml;
using PasheCPE.Almacenamiento;
using PasheCPE.Empaquetado;

namespace PasheCPE.Respuestas;

/// <summary>Constancia de recepción (CDR) leída.</summary>
internal sealed record CdrLeido(
    string Codigo,
    string? Descripcion,
    string? Referencia,
    DateOnly? Fecha,
    TimeOnly? Hora,
    IReadOnlyList<ObservacionCpe> Notas,
    string? EnlaceQr,
    bool? FirmaValida);

/// <summary>
/// Lectura del CDR (TRD §15): guarda <c>R-{nombre}.zip</c> íntegro en <c>RPTA\yyyy\MM</c>, lo abre en memoria (las
/// entradas que no son XML, como la carpeta <c>dummy/</c> de los CDR por SOAP, se ignoran) y lee con
/// <see cref="XmlReader"/> el código, la descripción, la referencia, la fecha y hora de respuesta, las notas
/// (<c>"4252 - texto"</c>) y, en guías, el enlace del QR. La firma de SUNAT se verifica solo si
/// <see cref="OpcionesPasheCpe.VerificarFirmaCdr"/>.
/// </summary>
internal sealed partial class LectorCdr(AlmacenCpe almacen, Microsoft.Extensions.Options.IOptions<OpcionesPasheCpe> opciones)
{
    private const string _cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    /// <summary>Guarda el ZIP del CDR y lo lee; si no se puede leer, el CDR igual queda guardado.</summary>
    public async Task<(CdrLeido? Cdr, string Ruta)> GuardarYLeerAsync(ReferenciaCpe referencia, byte[] zip, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(zip);
        var ruta = almacen.Ruta(CarpetaCpe.Rpta, referencia, NombreArchivoCpe.Cdr(referencia));
        await AlmacenCpe.EscribirAsync(ruta, zip, ct).ConfigureAwait(false);
        return (Leer(zip, opciones.Value.VerificarFirmaCdr), ruta);
    }

    /// <summary>Lee el CDR de un ZIP; null si el ZIP o el XML no son válidos. Nunca lanza.</summary>
    public static CdrLeido? Leer(byte[] zip, bool verificarFirma)
    {
        ArgumentNullException.ThrowIfNull(zip);
        byte[]? xml;
        try
        {
            xml = PrimerXml(zip);
        }
        catch (InvalidDataException)
        {
            return null;
        }

        if (xml is null)
        {
            return null;
        }

        try
        {
            return LeerXml(xml) is { } cdr ? cdr with { FirmaValida = verificarFirma ? FirmaValida(xml) : null } : null;
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static byte[]? PrimerXml(byte[] zip)
    {
        using var archivo = new ZipArchive(new MemoryStream(zip, writable: false), ZipArchiveMode.Read);
        var entrada = archivo.Entries.FirstOrDefault(e => e.Name.Length > 0 && e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        if (entrada is null)
        {
            return null;
        }

        using var flujo = entrada.Open();
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static CdrLeido? LeerXml(byte[] xml)
    {
        string? codigo = null, descripcion = null, referencia = null, fecha = null, hora = null, enlace = null;
        var notas = new List<ObservacionCpe>();
        using var lector = XmlReader.Create(new MemoryStream(xml, writable: false), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true });
        var avanzado = false;
        while (avanzado || lector.Read())
        {
            avanzado = false;
            if (lector.NodeType != XmlNodeType.Element || lector.NamespaceURI != _cbc || lector.IsEmptyElement)
            {
                continue;
            }

            switch (lector.LocalName)
            {
                case "ResponseCode" when codigo is null:
                    codigo = Texto(lector, ref avanzado);
                    break;
                case "Description" when descripcion is null:
                    descripcion = Texto(lector, ref avanzado);
                    break;
                case "ReferenceID" when referencia is null:
                    referencia = Texto(lector, ref avanzado);
                    break;
                case "ResponseDate" when fecha is null:
                    fecha = Texto(lector, ref avanzado);
                    break;
                case "ResponseTime" when hora is null:
                    hora = Texto(lector, ref avanzado);
                    break;
                case "DocumentDescription" when enlace is null:
                    enlace = Texto(lector, ref avanzado);
                    break;
                case "Note":
                    notas.Add(Nota(Texto(lector, ref avanzado)));
                    break;
            }
        }

        return codigo is null
            ? null
            : new CdrLeido(
                codigo,
                descripcion,
                referencia,
                DateOnly.TryParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null,
                TimeOnly.TryParse(hora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var h) ? h : null,
                notas,
                string.IsNullOrWhiteSpace(enlace) ? null : enlace,
                null);
    }

    /// <summary>«4252 - texto» → (4252, texto); una nota sin código conserva el texto con código vacío.</summary>
    private static ObservacionCpe Nota(string texto) => FormatoNota().Match(texto) is { Success: true } m
        ? new ObservacionCpe { Codigo = m.Groups[1].Value, Descripcion = m.Groups[2].Value.Trim() }
        : new ObservacionCpe { Codigo = string.Empty, Descripcion = texto };

    private static bool FirmaValida(byte[] xml)
    {
        try
        {
            var documento = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            using (var lector = XmlReader.Create(new MemoryStream(xml, writable: false), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
            {
                documento.Load(lector);
            }

            if (documento.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl) is not { Count: > 0 } firmas)
            {
                return false;
            }

            // SUNAT agrega dentro de Signature elementos fuera del estándar (Proposito, Revocacion, TSL, Expiracion) que
            // SignedXml rechaza. No están firmados: SignatureValue cubre solo SignedInfo y la transformación enveloped
            // quita todo el Signature del digest. Se retiran antes de verificar.
            var elemento = (XmlElement)firmas[0]!;
            foreach (var extra in elemento.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName is not ("SignedInfo" or "SignatureValue" or "KeyInfo" or "Object")).ToList())
            {
                elemento.RemoveChild(extra);
            }

            var firma = new SignedXml(documento);
            firma.LoadXml(elemento);
            return firma.CheckSignature();
        }
        catch (Exception ex) when (ex is CryptographicException or XmlException)
        {
            return false;
        }
    }

    private static string Texto(XmlReader lector, ref bool avanzado)
    {
        avanzado = true;
        return lector.ReadElementContentAsString().Trim();
    }

    [GeneratedRegex(@"^\s*(\d{3,4})\s*-\s*(.*)$", RegexOptions.Singleline)]
    private static partial Regex FormatoNota();
}
