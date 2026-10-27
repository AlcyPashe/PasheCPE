using PasheCPE.Tests.Utilidades;
using static PasheCPE.Tests.Utilidades.RespuestasGre;

namespace PasheCPE.Tests.Gre;

/// <summary>Generación y validación local de la guía remitente (ServicioGre y ValidadorGuia, PCPE-16xx).</summary>
public sealed class GeneracionGreTests : IDisposable
{
    private readonly ServiciosPrueba _servicios = new(o => o.Ambiente = Ambiente.Produccion);

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly Dictionary<string, (string Codigo, Func<GuiaRemisionCpe, GuiaRemisionCpe> Alterar)> _casos = new(StringComparer.Ordinal)
    {
        ["SerieSinT"] = ("PCPE-1601", g => g with { Serie = "F001" }),
        ["NumeroCero"] = ("PCPE-1602", g => g with { Numero = 0 }),
        ["EmisionFutura"] = ("PCPE-1603", g => g with { FechaEmision = CasosGre.Fecha.AddDays(1), FechaInicioTraslado = CasosGre.Fecha.AddDays(1) }),
        ["PlazoVencido"] = ("PCPE-1604", g => g with { FechaEmision = CasosGre.Fecha.AddDays(-2), FechaInicioTraslado = CasosGre.Fecha.AddDays(-2) }),
        ["MotivoOtrosSinDescripcion"] = ("PCPE-1605", g => g with { Motivo = MotivoTraslado.Otros, DescripcionMotivo = null }),
        ["PesoCero"] = ("PCPE-1606", g => g with { PesoBruto = 0 }),
        ["PesoCuatroDecimales"] = ("PCPE-1606", g => g with { PesoBruto = 1.2345m }),
        ["PesoEnLibras"] = ("PCPE-1606", g => g with { UnidadPeso = "LBR" }),
        ["SinBienes"] = ("PCPE-1607", g => g with { Bienes = [] }),
        ["BienSinCantidad"] = ("PCPE-1608", g => g with { Bienes = [g.Bienes[0] with { Cantidad = 0 }] }),
        ["PublicaSinTransportista"] = ("PCPE-1609", g => g with { Transportista = null }),
        ["PrivadaSinConductor"] = ("PCPE-1610", g => CasosGre.Privada() with { Conductores = [] }),
        ["CuatroVehiculos"] = ("PCPE-1611", g => CasosGre.Privada() with { Vehiculos = [.. Enumerable.Range(1, 4).Select(i => new VehiculoCpe { Placa = $"ABC12{i}" })] }),
        ["InicioAntesDeLaEmision"] = ("PCPE-1612", g => g with { FechaInicioTraslado = CasosGre.Fecha.AddDays(-1) }),
        ["MotivoInexistente"] = ("PCPE-1001", g => g with { Motivo = "99" }),
    };

    public static TheoryData<string> Casos => [.. _casos.Keys.Order(StringComparer.Ordinal)];

    [Fact]
    public async Task GenerarAsync_Guia_EscribeDataFirmaEnvioEHistorialSinQr()
    {
        var resultado = await Servicio().GenerarAsync(Emisor(), CasosGre.Dorada(), Ct);

        Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
        Assert.Equal("20123456786-09-T001-31093", resultado.Referencia.NombreArchivo);
        Assert.Equal(["4391"], resultado.Advertencias.Select(a => a.Codigo));
        Assert.All(new[] { resultado.Rutas.Xml, resultado.Rutas.XmlFirmado, resultado.Rutas.Zip }, ruta => Assert.True(File.Exists(ruta)));
        Assert.Null(resultado.Rutas.Qr);
        Assert.Equal(CasosGre.Fecha.AddDays(1), resultado.FechaLimiteEnvio);
        Assert.Equal(EstadoCpe.Generado, (await _servicios.Obtener<IHistorialCpe>().ObtenerAsync(resultado.Referencia, Ct))!.EstadoActual);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task ValidarAsync_DatoInvalido_DevuelveElCodigo(string caso)
    {
        var (codigo, alterar) = _casos[caso];

        var resultado = await Servicio().ValidarAsync(Emisor(), alterar(CasosGre.Dorada()), Ct);

        Assert.Contains(resultado.Errores, e => e.Codigo == codigo);
    }

    [Fact]
    public async Task ValidarAsync_VehiculoM1L_NoPideTransportista()
    {
        var resultado = await Servicio().ValidarAsync(Emisor(), CasosGre.Dorada() with
        {
            Transportista = null,
            Indicadores = new IndicadoresTrasladoCpe { VehiculoM1L = true },
        }, Ct);

        Assert.DoesNotContain(resultado.Errores, e => e.Codigo is "PCPE-1609" or "PCPE-1610");
    }

    [Fact]
    public async Task GenerarAsync_GuiaYaEnviada_NoSeRegenera()
    {
        var generada = await Servicio().GenerarAsync(Emisor(), CasosGre.Dorada(), Ct);
        _servicios.Sunat.Responder(Token, TokenOk()).Responder(Envio, Ticket("t1"));
        await Servicio().EnviarAsync(Emisor(), generada.Referencia, Ct);

        var otra = await Servicio().GenerarAsync(Emisor(), CasosGre.Dorada(), Ct);

        Assert.Equal("PCPE-3101", Assert.Single(otra.Errores).Codigo);
    }

    private IServicioGre Servicio() => _servicios.Obtener<IServicioGre>();

    private EmisorCpe Emisor() => CertificadoPrueba.Emisor(_servicios.Almacen);
}
