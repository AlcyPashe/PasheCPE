using System.Text.RegularExpressions;
using System.Xml;
using PasheCPE.Catalogos;

namespace PasheCPE.Transporte.Soap;

/// <summary>Fault SOAP con el código de SUNAT u OSE extraído y la entrada del catálogo de errores, si existe.</summary>
internal sealed record FaultSoap(string FaultCode, string FaultString, string? Detalle, string? Codigo, DatosError? Catalogo);

/// <summary>Respuesta de <c>getStatusCdr</c>.</summary>
internal sealed record EstadoCdrSoap(string? Codigo, string? Mensaje, string? ContenidoBase64);

/// <summary>Contenido útil de una respuesta SOAP: CDR de sendBill, estado de getStatusCdr o fault.</summary>
internal sealed record ContenidoSoap(string? ApplicationResponse, EstadoCdrSoap? StatusCdr, FaultSoap? Fault);

/// <summary>
/// Lee la respuesta SOAP con <see cref="XmlReader"/>, sin cargar el DOM (TRD §14): <c>applicationResponse</c> (ZIP del
/// CDR en Base64), <c>statusCdr/statusCode/statusMessage/content</c> o el fault. Devuelve null si el cuerpo no es un
/// sobre SOAP legible (proxy, página de error…). Nunca lanza.
/// </summary>
internal sealed partial class LectorRespuestaSoap(CatalogoErrores errores)
{
    private const string _sobreSoap = "http://schemas.xmlsoap.org/soap/envelope/";

    public ContenidoSoap? Leer(string? cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            return null;
        }

        string? applicationResponse = null, codigo = null, mensaje = null, contenido = null;
        string? faultCode = null, faultString = null, detalle = null;
        bool esSobre = false, enFault = false, enStatusCdr = false;
        try
        {
            using var lector = XmlReader.Create(new StringReader(cuerpo), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true });
            // ReadElementContentAsString y ReadInnerXml dejan el lector en el nodo siguiente: no se vuelve a llamar a Read.
            var avanzado = false;
            while (avanzado || lector.Read())
            {
                avanzado = false;
                if (lector.NodeType == XmlNodeType.EndElement)
                {
                    enFault &= !(lector.LocalName == "Fault" && lector.NamespaceURI == _sobreSoap);
                    enStatusCdr &= lector.LocalName != "statusCdr";
                    continue;
                }

                if (lector.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                switch (lector.LocalName)
                {
                    case "Envelope" when lector.NamespaceURI == _sobreSoap:
                        esSobre = true;
                        break;
                    case "Fault" when lector.NamespaceURI == _sobreSoap:
                        enFault = !lector.IsEmptyElement;
                        faultCode ??= string.Empty;
                        break;
                    case "faultcode" when enFault:
                        faultCode = Texto(lector, ref avanzado);
                        break;
                    case "faultstring" when enFault:
                        faultString = Texto(lector, ref avanzado);
                        break;
                    case "detail" when enFault:
                        if (!lector.IsEmptyElement)
                        {
                            detalle = lector.ReadInnerXml().Trim();
                            avanzado = true;
                        }

                        break;
                    case "applicationResponse":
                        applicationResponse = Texto(lector, ref avanzado);
                        break;
                    case "statusCdr":
                        enStatusCdr = !lector.IsEmptyElement;
                        break;
                    case "statusCode" when enStatusCdr:
                        codigo = Texto(lector, ref avanzado);
                        break;
                    case "statusMessage" when enStatusCdr:
                        mensaje = Texto(lector, ref avanzado);
                        break;
                    case "content" when enStatusCdr:
                        contenido = Texto(lector, ref avanzado);
                        break;
                }
            }
        }
        catch (XmlException)
        {
            return null;
        }

        if (!esSobre)
        {
            return null;
        }

        var fault = faultCode is null ? null : Fault(faultCode, faultString ?? string.Empty, string.IsNullOrEmpty(detalle) ? null : detalle);
        var statusCdr = codigo is null && mensaje is null && contenido is null ? null : new EstadoCdrSoap(codigo, mensaje, string.IsNullOrWhiteSpace(contenido) ? null : contenido);
        return new ContenidoSoap(string.IsNullOrWhiteSpace(applicationResponse) ? null : applicationResponse, statusCdr, fault);
    }

    /// <summary>
    /// Código del fault: sufijo numérico del <c>faultcode</c> (<c>soap-env:Client.0111</c> → 0111) o, si no lo tiene, el
    /// primer número del <c>faultstring</c>; con 4 dígitos.
    /// </summary>
    public FaultSoap Fault(string faultCode, string faultString, string? detalle)
    {
        var codigo = SufijoNumerico().Match(faultCode) is { Success: true } sufijo
            ? sufijo.Groups[1].Value
            : PrimerNumero().Match(faultString) is { Success: true } numero ? numero.Groups[1].Value : null;
        codigo = codigo?.TrimStart('0').PadLeft(4, '0');
        return new FaultSoap(faultCode, faultString, detalle, codigo, errores.Buscar(codigo));
    }

    private static string Texto(XmlReader lector, ref bool avanzado)
    {
        if (lector.IsEmptyElement)
        {
            return string.Empty;
        }

        avanzado = true;
        return lector.ReadElementContentAsString().Trim();
    }

    [GeneratedRegex(@"(\d{1,4})\s*$")]
    private static partial Regex SufijoNumerico();

    [GeneratedRegex(@"(?<!\d)(\d{3,4})(?!\d)")]
    private static partial Regex PrimerNumero();
}
