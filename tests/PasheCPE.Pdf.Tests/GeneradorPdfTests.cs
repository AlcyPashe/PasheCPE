using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasheCPE.Pdf.Tests.Apoyo;
using PasheCPE.Tests.Utilidades;
using QuestPDF.Helpers;
using static PasheCPE.Tests.Utilidades.RespuestasSoap;

namespace PasheCPE.Pdf.Tests;

public sealed class GeneradorPdfTests : IDisposable
{
    /// <summary>Marca de ejemplo con un logo de relleno de QuestPDF.</summary>
    private static readonly MarcaEmisor _marca = new()
    {
        Logo = Placeholders.Image(400, 200),
        ColorPrincipal = "#065986",
        Telefonos = ["(065) 123456", "987 654 321"],
        Correo = "ventas@ejemplo.pe",
        Web = "www.ejemplo.pe",
    };

    private readonly ServiciosPrueba _servicios = new(registrar: s => s.AddPasheCpePdf());

    public void Dispose() => _servicios.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IGeneradorPdf Generador => _servicios.Obtener<IGeneradorPdf>();

    public static TheoryData<string> Documentos => [.. DoradosPdf.Todos.Keys];

    /// <summary>
    /// Los seis documentos de la aceptación generan su PDF en REPO\yyyy\MM. Una copia queda en la carpeta Salida de las
    /// pruebas para la revisión visual del desarrollador.
    /// </summary>
    [Theory]
    [MemberData(nameof(Documentos))]
    public async Task GenerarAsync_Dorado_PdfEnRepo(string documento)
    {
        var dorado = DoradosPdf.Todos[documento];
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, dorado);
        var resultado = await Generador.GenerarAsync(referencia, _marca, Ct);

        Assert.True(resultado.Exito, string.Join("; ", resultado.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
        Assert.Empty(resultado.Advertencias);
        Assert.Equal(Path.Combine(_servicios.Almacen.Ruta, "REPO", DoradosPdf.Periodo(referencia), dorado.Nombre + ".pdf"), resultado.Ruta);
        var pdf = await File.ReadAllBytesAsync(resultado.Ruta!, Ct);
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);

        var salida = Path.Combine(AppContext.BaseDirectory, "Salida");
        Directory.CreateDirectory(salida);
        File.Copy(resultado.Ruta!, Path.Combine(salida, documento + ".pdf"), overwrite: true);
    }

    [Fact]
    public async Task GenerarAsync_SinMarca_PdfSinLogo()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.Boleta);

        var resultado = await Generador.GenerarAsync(referencia, MarcaEmisor.SinMarca, Ct);

        Assert.True(resultado.Exito);
    }

    [Fact]
    public async Task GenerarAsync_SeRegenera_ReemplazaElPdf()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.NotaCredito);

        var primero = await Generador.GenerarAsync(referencia, _marca, Ct);
        var segundo = await Generador.GenerarAsync(referencia, MarcaEmisor.SinMarca, Ct);

        Assert.Equal(primero.Ruta, segundo.Ruta);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(segundo.Ruta!)!));
    }

    [Fact]
    public async Task GenerarAsync_LogoNoValido_AdvertenciaYPdfSinLogo()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.FacturaExonerada);

        var resultado = await Generador.GenerarAsync(referencia, _marca with { Logo = "no es una imagen"u8.ToArray() }, Ct);

        Assert.True(resultado.Exito);
        Assert.Equal("PCPE-5007", Assert.Single(resultado.Advertencias).Codigo);
    }

    [Fact]
    public async Task GenerarAsync_RutaDeLogoInexistente_Advertencia()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.FacturaExonerada);

        var resultado = await Generador.GenerarAsync(referencia, new MarcaEmisor { RutaLogo = Path.Combine(_servicios.Almacen.Ruta, "no-existe.png") }, Ct);

        Assert.True(resultado.Exito);
        Assert.Equal("PCPE-5007", Assert.Single(resultado.Advertencias).Codigo);
    }

    [Fact]
    public async Task GenerarAsync_ColorNoValido_UsaElColorPorDefecto()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.FacturaExonerada);

        var resultado = await Generador.GenerarAsync(referencia, new MarcaEmisor { ColorPrincipal = "rojo" }, Ct);

        Assert.True(resultado.Exito);
    }

    [Fact]
    public async Task GenerarAsync_SinXmlFirmado_Pcpe5002()
    {
        var resultado = await Generador.GenerarAsync(Ejemplos.Referencia(), _marca, Ct);

        Assert.False(resultado.Exito);
        Assert.Equal("PCPE-5002", Assert.Single(resultado.Errores).Codigo);
        Assert.Null(resultado.Ruta);
    }

    [Fact]
    public async Task GenerarAsync_GuiaSinCdr_Pcpe5005()
    {
        var referencia = DoradosPdf.Colocar(_servicios.Almacen, DoradosPdf.Guia, conCdr: false);

        var resultado = await Generador.GenerarAsync(referencia, _marca, Ct);

        Assert.Equal("PCPE-5005", Assert.Single(resultado.Errores).Codigo);
    }

    [Theory]
    [InlineData("RC")]
    [InlineData("RA")]
    [InlineData("31")]
    public async Task GenerarAsync_TipoSinDiseno_Pcpe5004(string tipo)
    {
        var referencia = tipo switch
        {
            "RC" => ReferenciaCpe.Resumen(Ejemplos.Ruc, new DateOnly(2026, 10, 22), 1),
            "RA" => ReferenciaCpe.Baja(Ejemplos.Ruc, new DateOnly(2026, 10, 22), 1),
            _ => ReferenciaCpe.Guia(Ejemplos.Ruc, tipo, "V001", 1, new DateOnly(2026, 10, 22)),
        };

        var resultado = await Generador.GenerarAsync(referencia, _marca, Ct);

        Assert.Equal("PCPE-5004", Assert.Single(resultado.Errores).Codigo);
    }

    [Fact]
    public async Task PasheCpePdf_SinDi_GeneraConElMismoContenedor()
    {
        using var almacen = new AlmacenTemporal();
        var referencia = DoradosPdf.Colocar(almacen, DoradosPdf.NotaDebito);
        using var pashe = PasheCpePdf.Crear(almacen.Opciones(o => o.PrecalentarAlIniciar = false));

        var resultado = await pashe.Pdf().GenerarAsync(referencia, _marca, Ct);

        Assert.True(resultado.Exito);
    }

    [Fact]
    public void Pdf_InstanciaSinPdf_ExcepcionDeUso()
    {
        using var almacen = new AlmacenTemporal();
        using var pashe = PasheCpe.Crear(almacen.Opciones(o => o.PrecalentarAlIniciar = false));

        Assert.Throws<InvalidOperationException>(() => pashe.Pdf());
    }

    [Fact]
    public async Task AddPasheCpePdf_DesdeLaConfiguracion_MarcasPorRucYRutaLogoVacia()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MarcaPorDefecto:RutaLogo"] = string.Empty,
                ["MarcaPorDefecto:ColorPrincipal"] = "#336699",
                ["Marcas:20493400364:Web"] = "www.jully.pe",
                ["Marcas:20493400364:Telefonos:0"] = "065-111111",
            })
            .Build();
        using var servicios = new ServiciosPrueba(registrar: s => s.AddPasheCpePdf(configuracion));
        var opciones = servicios.Obtener<Microsoft.Extensions.Options.IOptions<OpcionesPasheCpePdf>>().Value;
        var referencia = DoradosPdf.Colocar(servicios.Almacen, DoradosPdf.NotaDebito);

        var resultado = await servicios.Obtener<IGeneradorPdf>().GenerarAsync(referencia, opciones.MarcaDe("20493400364"), Ct);

        Assert.Equal(("www.jully.pe", "065-111111"), (opciones.MarcaDe("20493400364").Web, opciones.MarcaDe("20493400364").Telefonos.Single()));
        Assert.Equal("#336699", opciones.MarcaDe(Ejemplos.Ruc).ColorPrincipal);
        Assert.True(resultado.Exito);
        Assert.Empty(resultado.Advertencias);
        Assert.Empty((await servicios.Obtener<IGeneradorPdf>().GenerarAsync(referencia, opciones.MarcaDe(Ejemplos.Ruc), Ct)).Advertencias);
    }

    [Fact]
    public void OpcionesPdf_MarcaPorRuc_PorDefectoOSinMarca()
    {
        var porRuc = new MarcaEmisor { Web = "a" };
        var porDefecto = new MarcaEmisor { Web = "b" };
        var opciones = new OpcionesPasheCpePdf { Marcas = { ["20493400364"] = porRuc } };

        Assert.Same(porRuc, opciones.MarcaDe("20493400364"));
        Assert.Same(MarcaEmisor.SinMarca, opciones.MarcaDe(Ejemplos.Ruc));
        opciones.MarcaPorDefecto = porDefecto;
        Assert.Same(porDefecto, opciones.MarcaDe(Ejemplos.Ruc));
    }
}
