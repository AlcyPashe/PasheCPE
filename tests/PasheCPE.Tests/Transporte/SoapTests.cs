using System.Xml.Linq;
using PasheCPE.Diagnostico;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Transporte.Soap;

namespace PasheCPE.Tests.Transporte;

public sealed class SoapTests : IDisposable
{
    private static readonly XNamespace _wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace _ser = "http://service.sunat.gob.pe";

    private readonly ServiciosPrueba _servicios = new();

    public void Dispose() => _servicios.Dispose();

    private LectorRespuestaSoap Lector => _servicios.Obtener<LectorRespuestaSoap>();

    [Fact]
    public void SendBill_SobreConUsuarioRucMasSolYValoresEscapados()
    {
        var usuario = ConstructorSobreSoap.UsuarioSoap(Ejemplos.Emisor(), new CredencialesSoap("MODDATOS", "x"));

        var sobre = XDocument.Parse(ConstructorSobreSoap.SendBill(usuario, "cl<a&ve\"", "20123456786-01-F001-1.zip", "UEsDBA=="));

        Assert.Equal("20123456786MODDATOS", sobre.Descendants(_wsse + "Username").Single().Value);
        Assert.Equal("cl<a&ve\"", sobre.Descendants(_wsse + "Password").Single().Value);
        var operacion = sobre.Descendants(_ser + "sendBill").Single();
        Assert.Equal("20123456786-01-F001-1.zip", operacion.Element("fileName")!.Value);
        Assert.Equal("UEsDBA==", operacion.Element("contentFile")!.Value);
    }

    [Fact]
    public void UsuarioSoap_Ose_UsaElUsuarioTalCual()
    {
        var emisor = Ejemplos.Emisor() with { Canal = CanalEnvio.Ose("efact") };

        Assert.Equal("usuario-ose", ConstructorSobreSoap.UsuarioSoap(emisor, new CredencialesSoap("usuario-ose", "x")));
    }

    [Fact]
    public void GetStatusCdr_SobreConLosCuatroDatosDelComprobante()
    {
        var sobre = XDocument.Parse(ConstructorSobreSoap.GetStatusCdr("20123456786MODDATOS", "x", Ejemplos.Referencia(45)));

        var operacion = sobre.Descendants(_ser + "getStatusCdr").Single();
        Assert.Equal(["20123456786", "01", "F001", "45"], operacion.Elements().Select(e => e.Value));
    }

    [Fact]
    public void Leer_RespuestaSendBill_DevuelveElCdr()
    {
        var contenido = Lector.Leer(RespuestasSoap.ConCdr([1, 2, 3]));

        Assert.Equal("AQID", contenido!.ApplicationResponse);
        Assert.Null(contenido.Fault);
    }

    [Theory]
    [InlineData("soap-env:Client.0111", "No tiene el perfil para enviar comprobantes electronicos", "0111")]
    [InlineData("soap-env:Client.1033", "El comprobante fue registrado previamente con otros datos - Detalle: xxx", "1033")]
    [InlineData("soap-env:Client.2335", "El documento electrónico ingresado ha sido alterado", "2335")]
    [InlineData("soap-env:Server", "0100 - El sistema no puede responder su solicitud", "0100")]
    [InlineData("soap-env:Client.100", "El sistema no puede responder su solicitud", "0100")]
    public void Leer_Fault_CodigoDelFaultcodeODelFaultstring(string faultCode, string faultString, string esperado)
    {
        var fault = Lector.Leer(RespuestasSoap.Fault(faultCode, faultString, "detalle técnico"))!.Fault!;

        Assert.Equal(esperado, fault.Codigo);
        Assert.Equal(faultString, fault.FaultString);
        Assert.Equal("<message>detalle técnico</message>", fault.Detalle);
        Assert.NotNull(fault.Catalogo);
    }

    [Fact]
    public void Leer_Fault0100_EsTransitorioDeDisponibilidad()
    {
        var fault = Lector.Leer(RespuestasSoap.Fault("soap-env:Client.0100", "El sistema no puede responder su solicitud"))!.Fault!;

        Assert.True(fault.Catalogo!.Transitorio);
        Assert.Equal("disponibilidad", fault.Catalogo.GrupoTransitorio);
    }

    [Fact]
    public void Leer_StatusCdr_CodigoMensajeYContenido()
    {
        var contenido = Lector.Leer(RespuestasSoap.StatusCdr("0001", "La constancia existe", [9, 9]))!;

        Assert.Equal(new EstadoCdrSoap("0001", "La constancia existe", "CQk="), contenido.StatusCdr);
    }

    [Fact]
    public void Leer_GetStatusCdrDeEfact_ElDocumentoEsElContenidoSinCodigo()
    {
        var contenido = Lector.Leer(RespuestasSoap.DocumentoEfact([9, 9]))!;

        Assert.Equal(new EstadoCdrSoap(null, null, "CQk="), contenido.StatusCdr);
        Assert.Null(contenido.ApplicationResponse);
    }

    [Fact]
    public void Leer_GetStatusDeEfact_IgnoraContentMessage()
    {
        var contenido = Lector.Leer(RespuestasSoap.StatusEfact("0", [9, 9]))!;

        Assert.Equal(new EstadoTicketSoap("0", "CQk="), contenido.Status);
        Assert.Null(contenido.StatusCdr);
    }

    /// <summary>Formas de fault de un OSE (prefijos S: o env:, código solo en el faultstring).</summary>
    [Theory]
    [InlineData("S:Server", "2335 - El documento electrónico ingresado ha sido alterado", "2335")]
    [InlineData("env:Client.0111", "No tiene el perfil para enviar comprobantes electronicos", "0111")]
    [InlineData("S:Client", "El comprobante fue registrado previamente con otros datos (1033)", "1033")]
    public void Leer_FaultDeOse_CodigoDelFaultcodeODelFaultstring(string faultCode, string faultString, string esperado)
    {
        Assert.Equal(esperado, Lector.Leer(RespuestasSoap.Fault(faultCode, faultString))!.Fault!.Codigo);
    }

    [Fact]
    public void Enmascarador_TruncaElDocumentoDeGetStatusCdr()
    {
        var cdr = Convert.ToBase64String(new byte[300]);

        var enmascarado = Enmascarador.Sobre($"<document>{cdr}</document>");

        Assert.DoesNotContain(cdr, enmascarado, StringComparison.Ordinal);
        Assert.Contains($"({cdr.Length} caracteres)", enmascarado, StringComparison.Ordinal);
    }

    [Fact]
    public void SendSummaryYGetStatus_SobresConElArchivoYElTicketEscapados()
    {
        var envio = XDocument.Parse(ConstructorSobreSoap.SendSummary("20123456786MODDATOS", "x", "20123456786-RC-20261022-1.zip", "UEsDBA=="));
        var consulta = XDocument.Parse(ConstructorSobreSoap.GetStatus("20123456786MODDATOS", "x", "17<29"));

        var operacion = envio.Descendants(_ser + "sendSummary").Single();
        Assert.Equal(["20123456786-RC-20261022-1.zip", "UEsDBA=="], operacion.Elements().Select(e => e.Value));
        Assert.Equal("17<29", consulta.Descendants(_ser + "getStatus").Single().Element("ticket")!.Value);
    }

    [Fact]
    public void Leer_RespuestaSendSummary_DevuelveElTicket()
    {
        var contenido = Lector.Leer(RespuestasSoap.Ticket("1729876543210"))!;

        Assert.Equal("1729876543210", contenido.Ticket);
        Assert.Null(contenido.Status);
        Assert.Null(contenido.StatusCdr);
    }

    [Theory]
    [InlineData("98", false)]
    [InlineData("0", true)]
    [InlineData("99", true)]
    public void Leer_RespuestaGetStatus_DevuelveElEstadoYElCdr(string codigo, bool conCdr)
    {
        var contenido = Lector.Leer(RespuestasSoap.Status(codigo, conCdr ? [9, 9] : null))!;

        Assert.Equal(new EstadoTicketSoap(codigo, conCdr ? "CQk=" : null), contenido.Status);
        Assert.Null(contenido.StatusCdr);
        Assert.Null(contenido.Ticket);
    }

    [Theory]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    [InlineData("no es xml")]
    [InlineData("")]
    public void Leer_CuerpoQueNoEsSoap_DevuelveNull(string cuerpo)
    {
        Assert.Null(Lector.Leer(cuerpo));
    }

    [Fact]
    public void Enmascarador_OcultaLaClaveYTruncaElBase64()
    {
        var zip = Convert.ToBase64String(new byte[300]);
        var sobre = ConstructorSobreSoap.SendBill("20123456786MODDATOS", "clave-secreta", "a.zip", zip);

        var enmascarado = Enmascarador.Sobre(sobre);

        Assert.DoesNotContain("clave-secreta", enmascarado, StringComparison.Ordinal);
        Assert.Contains("<wsse:Password>***</wsse:Password>", enmascarado, StringComparison.Ordinal);
        Assert.DoesNotContain(zip, enmascarado, StringComparison.Ordinal);
        Assert.Contains($"({zip.Length} caracteres)", enmascarado, StringComparison.Ordinal);
        Assert.Contains("20123456786MODDATOS", enmascarado, StringComparison.Ordinal);
    }
}
