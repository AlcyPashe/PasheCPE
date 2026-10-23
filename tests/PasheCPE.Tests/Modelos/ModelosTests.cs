using System.Text.Json;
using PasheCPE.Tests.Utilidades;

namespace PasheCPE.Tests.Modelos;

public sealed class ModelosTests
{
    [Fact]
    public void ComprobanteCpe_IdaYVueltaJson_ConservaLosDatos()
    {
        var factura = Ejemplos.Factura();

        var json = JsonSerializer.Serialize(factura, PasheCpeJson.Default.ComprobanteCpe);
        var leida = JsonSerializer.Deserialize(json, PasheCpeJson.Default.ComprobanteCpe);

        Assert.NotNull(leida);
        Assert.Equal(factura.Serie, leida.Serie);
        Assert.Equal(factura.Lineas, leida.Lineas);
        Assert.Equal(factura.FormaPago!.Cuotas, leida.FormaPago!.Cuotas);
        Assert.Contains("\"modoPrecio\":\"PrecioUnitario\"", json, StringComparison.Ordinal);
        Assert.Contains("\"tipoOperacion\":\"0101\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ResultadoEnvio_IdaYVueltaJson_EnumsComoTexto()
    {
        var resultado = new ResultadoEnvio
        {
            Estado = EstadoCpe.AceptadoConObservaciones,
            Codigo = "0",
            Observaciones = [new ObservacionCpe { Codigo = "4252", Descripcion = "Observación" }],
            FechaLimiteEnvio = new DateOnly(2026, 10, 25),
            FechaProceso = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.FromHours(-5)),
        };

        var json = JsonSerializer.Serialize(resultado, PasheCpeJson.Default.ResultadoEnvio);
        var leido = JsonSerializer.Deserialize(json, PasheCpeJson.Default.ResultadoEnvio);

        Assert.Contains("\"estado\":\"AceptadoConObservaciones\"", json, StringComparison.Ordinal);
        Assert.Contains("\"nivel\":3", json, StringComparison.Ordinal);
        Assert.Equal(resultado.Estado, leido?.Estado);
        Assert.Equal(resultado.Observaciones, leido?.Observaciones);
        Assert.Equal(resultado.FechaProceso, leido?.FechaProceso);
    }

    [Fact]
    public void Emisor_SerializadoPorReflexion_NoIncluyeClaves()
    {
        var json = JsonSerializer.Serialize(Ejemplos.Emisor());

        Assert.DoesNotContain("secreta", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto-cliente", json, StringComparison.Ordinal);
        Assert.Contains("MODDATOS", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Emisor_ToString_EnmascaraLasClaves()
    {
        var texto = Ejemplos.Emisor().ToString();

        Assert.DoesNotContain("secreta", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto-cliente", texto, StringComparison.Ordinal);
        Assert.Contains("Clave = ***", texto, StringComparison.Ordinal);
        Assert.Contains("ClientSecret = ***", texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EstadoCpe.SinConexionInternet, 0)]
    [InlineData(EstadoCpe.ServicioNoDisponible, 0)]
    [InlineData(EstadoCpe.ErrorValidacionLocal, 1)]
    [InlineData(EstadoCpe.ErrorServicio, 2)]
    [InlineData(EstadoCpe.RespuestaIncierta, 2)]
    [InlineData(EstadoCpe.EnProceso, 3)]
    [InlineData(EstadoCpe.Aceptado, 3)]
    [InlineData(EstadoCpe.Rechazado, 3)]
    [InlineData(EstadoCpe.Generado, null)]
    public void Nivel_SegunEstado(EstadoCpe estado, int? nivel) => Assert.Equal(nivel, estado.Nivel());

    [Fact]
    public void ReferenciaCpe_NombresDeArchivo()
    {
        Assert.Equal("20123456786-01-F001-123", Ejemplos.Referencia().NombreArchivo);
        Assert.Equal("20123456786-RC-20261023-2", ReferenciaCpe.Resumen(Ejemplos.Ruc, new DateOnly(2026, 10, 23), 2).NombreArchivo);
        Assert.Equal("20123456786-RA-20261023-1", ReferenciaCpe.Baja(Ejemplos.Ruc, new DateOnly(2026, 10, 23), 1).NombreArchivo);
        Assert.Equal("20123456786-09-T001-45", ReferenciaCpe.Guia(Ejemplos.Ruc, TipoDocumentoSunat.GuiaDeRemisionRemitente, "T001", 45, new DateOnly(2026, 10, 23)).NombreArchivo);
        Assert.Equal("R-20123456786-01-F001-123.zip", PasheCPE.Empaquetado.NombreArchivoCpe.Cdr(Ejemplos.Referencia()));
    }

    [Fact]
    public void CanalEnvio_Clave()
    {
        Assert.Equal("SUNAT", CanalEnvio.Sunat.Clave);
        Assert.Equal("OSE:efact", CanalEnvio.Ose(" efact ").Clave);
    }
}
