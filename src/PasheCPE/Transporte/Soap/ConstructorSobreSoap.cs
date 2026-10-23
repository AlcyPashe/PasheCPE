using System.Security;

namespace PasheCPE.Transporte.Soap;

/// <summary>Operaciones SOAP de SUNAT (billService y billConsultService).</summary>
internal static class OperacionSoap
{
    public const string SendBill = "sendBill";
    public const string GetStatusCdr = "getStatusCdr";
}

/// <summary>
/// Sobres SOAP 1.1 con <c>wsse:UsernameToken</c> (TRD §14). El usuario es RUC + usuario secundario SOL para SUNAT y
/// el usuario asignado por el OSE para un OSE; la clave va en texto. Todos los valores se escapan.
/// </summary>
internal static class ConstructorSobreSoap
{
    public static string UsuarioSoap(EmisorCpe emisor, CredencialesSoap credenciales)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(credenciales);
        return emisor.Canal.Tipo == TipoCanal.Sunat ? emisor.Ruc + credenciales.Usuario : credenciales.Usuario;
    }

    public static string SendBill(string usuario, string clave, string nombreZip, string zipBase64) => Sobre(usuario, clave, $"""
        <ser:sendBill><fileName>{E(nombreZip)}</fileName><contentFile>{zipBase64}</contentFile></ser:sendBill>
        """);

    public static string GetStatusCdr(string usuario, string clave, ReferenciaCpe referencia)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        return Sobre(usuario, clave, $"""
            <ser:getStatusCdr><rucComprobante>{E(referencia.Ruc)}</rucComprobante><tipoComprobante>{E(referencia.TipoDocumento)}</tipoComprobante><serieComprobante>{E(referencia.Serie ?? string.Empty)}</serieComprobante><numeroComprobante>{referencia.Numero}</numeroComprobante></ser:getStatusCdr>
            """);
    }

    private static string Sobre(string usuario, string clave, string cuerpo) => $"""
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:ser="http://service.sunat.gob.pe" xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"><soapenv:Header><wsse:Security><wsse:UsernameToken><wsse:Username>{E(usuario)}</wsse:Username><wsse:Password>{E(clave)}</wsse:Password></wsse:UsernameToken></wsse:Security></soapenv:Header><soapenv:Body>{cuerpo}</soapenv:Body></soapenv:Envelope>
        """;

    private static string E(string valor) => SecurityElement.Escape(valor) ?? string.Empty;
}
