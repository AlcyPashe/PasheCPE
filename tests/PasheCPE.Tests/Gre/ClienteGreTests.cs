using PasheCPE.Diagnostico;
using PasheCPE.Transporte.Gre;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Gre;

/// <summary>Lectura tolerante de las respuestas JSON de la API de guías y enmascarado del log.</summary>
public sealed class ClienteGreTests
{
    [Fact]
    public void LeerEstado_NumErrorComoNumero_LoLeeComoTexto()
    {
        var estado = ClienteGre.LeerEstado(RespuestasGre.Estado("99", numError: 2335, desError: "Alterado"))!;

        Assert.Equal(("99", "2335", "Alterado", null), (estado.CodRespuesta, estado.NumError, estado.DesError, estado.ArcCdr));
    }

    [Fact]
    public void LeerError_422_ErroresFuncionales()
    {
        var error = ClienteGre.LeerError(RespuestasGre.Error("422", "Unprocessable Entity", ("166", "Código de ticket no enviado.")))!;

        Assert.Equal(("422", "166", "Código de ticket no enviado."), (error.Cod, error.Errores[0].Codigo, error.Errores[0].Descripcion));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>502</html>")]
    [InlineData("[1,2]")]
    public void Leer_CuerpoQueNoEsUnObjetoJson_DevuelveNull(string cuerpo)
    {
        Assert.Null(ClienteGre.LeerToken(cuerpo));
        Assert.Null(ClienteGre.LeerEstado(cuerpo));
        Assert.Null(ClienteGre.LeerError(cuerpo));
    }

    [Fact]
    public void Enmascarador_OcultaElTokenYTruncaElZipDelJson()
    {
        var zip = Convert.ToBase64String(new byte[300]);
        var json = "{\"access_token\":\"secreto-del-token\",\"archivo\":{\"arcGreZip\":\"" + zip + "\"}}";

        var enmascarado = Enmascarador.Sobre(json);

        Assert.DoesNotContain("secreto-del-token", enmascarado, StringComparison.Ordinal);
        Assert.DoesNotContain(zip, enmascarado, StringComparison.Ordinal);
        Assert.Contains($"({zip.Length} caracteres)", enmascarado, StringComparison.Ordinal);
    }

    [Fact]
    public void HashZip_Sha256HexadecimalEnMinusculas() =>
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ClienteGre.HashZip("abc"u8.ToArray()));
}
