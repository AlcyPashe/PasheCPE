using System.IO.Compression;
using System.Text;
using PasheCPE.Tests.Dorados;

namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// Respuestas SOAP sintéticas con la estructura que documenta SUNAT (prefijo <c>soap-env</c>, como en sus
/// respuestas). Los CDR son los dorados reales o CDR mínimos armados aquí.
/// </summary>
internal static class RespuestasSoap
{
    public const string SendBill = "sendBill";
    public const string GetStatusCdr = "getStatusCdr";
    public const string SendSummary = "sendSummary";
    public const string GetStatus = "getStatus";

    public static string ConCdr(byte[] cdrZip) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><soap-env:Envelope xmlns:soap-env="http://schemas.xmlsoap.org/soap/envelope/"><soap-env:Header/><soap-env:Body><br:sendBillResponse xmlns:br="http://service.sunat.gob.pe"><applicationResponse>{Convert.ToBase64String(cdrZip)}</applicationResponse></br:sendBillResponse></soap-env:Body></soap-env:Envelope>""";

    public static string Fault(string faultCode, string faultString, string? detalle = null) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><soap-env:Envelope xmlns:soap-env="http://schemas.xmlsoap.org/soap/envelope/"><soap-env:Header/><soap-env:Body><soap-env:Fault><faultcode>{faultCode}</faultcode><faultstring>{faultString}</faultstring>{(detalle is null ? string.Empty : $"<detail><message>{detalle}</message></detail>")}</soap-env:Fault></soap-env:Body></soap-env:Envelope>""";

    public static string StatusCdr(string codigo, string mensaje, byte[]? cdrZip = null) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><soap-env:Envelope xmlns:soap-env="http://schemas.xmlsoap.org/soap/envelope/"><soap-env:Header/><soap-env:Body><ns2:getStatusCdrResponse xmlns:ns2="http://service.sunat.gob.pe"><statusCdr>{(cdrZip is null ? string.Empty : $"<content>{Convert.ToBase64String(cdrZip)}</content>")}<statusCode>{codigo}</statusCode><statusMessage>{mensaje}</statusMessage></statusCdr></ns2:getStatusCdrResponse></soap-env:Body></soap-env:Envelope>""";

    /// <summary>Respuesta de sendSummary con el ticket.</summary>
    public static string Ticket(string ticket) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><soap-env:Envelope xmlns:soap-env="http://schemas.xmlsoap.org/soap/envelope/"><soap-env:Header/><soap-env:Body><br:sendSummaryResponse xmlns:br="http://service.sunat.gob.pe"><ticket>{ticket}</ticket></br:sendSummaryResponse></soap-env:Body></soap-env:Envelope>""";

    /// <summary>Respuesta de getStatus: estado del ticket (0, 98, 99) y el CDR, si lo hay.</summary>
    public static string Status(string codigo, byte[]? cdrZip = null) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><soap-env:Envelope xmlns:soap-env="http://schemas.xmlsoap.org/soap/envelope/"><soap-env:Header/><soap-env:Body><br:getStatusResponse xmlns:br="http://service.sunat.gob.pe"><status>{(cdrZip is null ? string.Empty : $"<content>{Convert.ToBase64String(cdrZip)}</content>")}<statusCode>{codigo}</statusCode></status></br:getStatusResponse></soap-env:Body></soap-env:Envelope>""";

    /// <summary>getStatusCdr de eFact (WSDL de ose.efact.pe): el ZIP del CDR directamente en <c>document</c>.</summary>
    public static string DocumentoEfact(byte[] cdrZip) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:getStatusCdrResponse xmlns:ns2="http://service.sunat.gob.pe"><document>{Convert.ToBase64String(cdrZip)}</document></ns2:getStatusCdrResponse></S:Body></S:Envelope>""";

    /// <summary>getStatus de eFact: statusResponse con <c>content</c>, <c>contentMessage</c> y <c>statusCode</c>.</summary>
    public static string StatusEfact(string codigo, byte[]? cdrZip = null) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><S:Envelope xmlns:S="http://schemas.xmlsoap.org/soap/envelope/"><S:Body><ns2:getStatusResponse xmlns:ns2="http://service.sunat.gob.pe"><status>{(cdrZip is null ? string.Empty : $"<content>{Convert.ToBase64String(cdrZip)}</content>")}<contentMessage>Procesado</contentMessage><statusCode>{codigo}</statusCode></status></ns2:getStatusResponse></S:Body></S:Envelope>""";

    /// <summary>CDR dorado real de un resumen diario aceptado.</summary>
    public static byte[] CdrDoradoResumen() =>
        File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta("RC-Resumen"), "R-20611619767-RC-20261022-7.zip"));

    /// <summary>CDR dorado real: aceptado, sin notas, con la carpeta dummy/.</summary>
    public static byte[] CdrDoradoAceptado() =>
        File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta("01-Factura"), "R-20611619767-01-F001-3565.zip"));

    /// <summary>CDR dorado real de una GRE aceptada con dos observaciones (4391 y 4382).</summary>
    public static byte[] CdrDoradoObservado() =>
        File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta("09-GRE"), "R-20611619767-09-T001-31093.zip"));

    /// <summary>CDR mínimo con el código, la descripción y las notas indicadas.</summary>
    public static byte[] Cdr(string referencia, string codigo, string descripcion, params string[] notas)
    {
        var xml = $"""<?xml version="1.0" encoding="UTF-8"?><ar:ApplicationResponse xmlns:ar="urn:oasis:names:specification:ubl:schema:xsd:ApplicationResponse-2" xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2" xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"><cbc:ResponseDate>2026-10-22</cbc:ResponseDate><cbc:ResponseTime>10:05:00</cbc:ResponseTime>{string.Concat(notas.Select(n => $"<cbc:Note>{n}</cbc:Note>"))}<cac:DocumentResponse><cac:Response><cbc:ReferenceID>{referencia}</cbc:ReferenceID><cbc:ResponseCode>{codigo}</cbc:ResponseCode><cbc:Description>{descripcion}</cbc:Description></cac:Response></cac:DocumentResponse></ar:ApplicationResponse>""";
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("dummy/");
            using var flujo = zip.CreateEntry($"R-{referencia}.xml").Open();
            flujo.Write(Encoding.UTF8.GetBytes(xml));
        }

        return memoria.ToArray();
    }
}
