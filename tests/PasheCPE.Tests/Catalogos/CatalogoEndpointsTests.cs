using Microsoft.Extensions.Logging;
using PasheCPE.Catalogos;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Catalogos;

public sealed class CatalogoEndpointsTests : IDisposable
{
    private readonly AlmacenTemporal _almacen = new();
    private readonly RegistroFalso<CatalogoEndpoints> _registro = new();

    public void Dispose() => _almacen.Dispose();

    [Fact]
    public void Obtener_SunatBeta_DevuelveBillServiceVerificado()
    {
        var endpoint = Catalogo().Obtener(CanalEnvio.Sunat, Ambiente.Beta, ServicioSunat.Comprobantes);

        Assert.Equal("https://e-beta.sunat.gob.pe/ol-ti-itcpfegem-beta/billService", endpoint?.Url);
        Assert.True(endpoint?.Verificado);
        Assert.Empty(_registro.Eventos);
    }

    [Fact]
    public void Obtener_EndpointNoVerificado_AdvierteUnaSolaVez()
    {
        var catalogo = Catalogo();

        var endpoint = catalogo.Obtener(CanalEnvio.Sunat, Ambiente.Produccion, ServicioSunat.ConsultaCdr);
        catalogo.Obtener(CanalEnvio.Sunat, Ambiente.Produccion, ServicioSunat.ConsultaCdr);

        Assert.False(endpoint?.Verificado);
        var evento = Assert.Single(_registro.Eventos);
        Assert.Equal(LogLevel.Warning, evento.Nivel);
        Assert.Contains("billConsultService", evento.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public void Obtener_GetStatusCdrEnBeta_NoExiste() =>
        Assert.Null(Catalogo().Obtener(CanalEnvio.Sunat, Ambiente.Beta, ServicioSunat.ConsultaCdr));

    [Fact]
    public void Obtener_OseSinConfigurar_EsNull() =>
        Assert.Null(Catalogo().Obtener(CanalEnvio.Ose("efact"), Ambiente.Produccion, ServicioSunat.Comprobantes));

    [Fact]
    public void Obtener_EndpointConfigurado_TienePrioridad()
    {
        var catalogo = Catalogo(o =>
        {
            o.Endpoints.Add(new EndpointCpe { Canal = "OSE:efact", Ambiente = Ambiente.Produccion, Servicio = ServicioSunat.Comprobantes, Url = "https://ose.ejemplo.pe/billService" });
            o.Endpoints.Add(new EndpointCpe { Canal = "SUNAT", Ambiente = Ambiente.Beta, Servicio = ServicioSunat.Comprobantes, Url = "https://beta.ejemplo.pe/billService" });
        });

        Assert.Equal("https://ose.ejemplo.pe/billService", catalogo.Obtener(CanalEnvio.Ose("efact"), Ambiente.Produccion, ServicioSunat.Comprobantes)?.Url);
        Assert.Equal("https://beta.ejemplo.pe/billService", catalogo.Obtener(CanalEnvio.Sunat, Ambiente.Beta, ServicioSunat.Comprobantes)?.Url);
    }

    private CatalogoEndpoints Catalogo(Action<OpcionesPasheCpe>? configurar = null) => new(_almacen.OpcionesDi(configurar), _registro);
}
