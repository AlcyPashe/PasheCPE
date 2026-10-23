using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using PasheCPE.Tests.Utilidades;
using PasheCPE.Tiempo;
using PasheCPE.Validacion;
using PasheCPE.Validacion.Emulacion;
using static PasheCPE.Tests.Utilidades.XmlDePrueba;

namespace PasheCPE.Tests.Validacion;

/// <summary>Emulación de DataPower: extensiones, resolutor, correcciones de compatibilidad, fallas, concurrencia y tiempos.</summary>
public sealed class EmulacionDataPowerTests
{
    [Fact]
    public void Fechas_Date_UsaLaFechaDeLima()
    {
        // 03:00 UTC del 23/10 son las 22:00 del 22/10 en Lima.
        using var servicios = new ServiciosPrueba(ahora: new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero));
        var fechas = new ExtensionesExsltFechas(servicios.Obtener<RelojCpe>().AhoraLima());

        Assert.Equal("2026-10-22-05:00", fechas.date());
        Assert.Equal(86_400d, fechas.seconds("1970-01-02"));
        Assert.True(double.IsNaN(fechas.seconds("22/10/2026")));
    }

    [Fact]
    public void Regexp_TestMatchYReplace()
    {
        var regexp = new ExtensionesExsltRegexp();

        Assert.True(regexp.test("F001", "^[F][A-Z0-9]{3}$"));
        Assert.False(regexp.test("f001", "^[F][A-Z0-9]{3}$"));
        Assert.True(regexp.test("f001", "^[F][A-Z0-9]{3}$", "i"));
        var grupos = regexp.match("F001-123", "^([A-Z0-9]{4})-([0-9]+)$");
        var valores = new List<string>();
        while (grupos.MoveNext())
        {
            valores.Add(grupos.Current!.Value);
        }

        Assert.Equal(["F001-123", "F001", "123"], valores);
        Assert.Equal("F001_123", regexp.replace("F001-123", "-", string.Empty, "_"));
    }

    [Fact]
    public void Regexp_ExpresionCatastrofica_CortaPorTiempo()
    {
        var regexp = new ExtensionesExsltRegexp();

        Assert.Throws<RegexMatchTimeoutException>(() => regexp.test(new string('a', 40) + "!", "^(a+)+$"));
    }

    [Theory]
    [InlineData("http://www.sunat.gob.pe/validate_utils.xsl")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("local:///commons/otro.xsl")]
    public void Resolutor_UriNoEmbebida_SeRechaza(string uri)
    {
        Assert.Throws<XmlException>(() => ResolutorRecursosLocales.Instancia.GetEntity(new Uri(uri), null, typeof(Stream)));
    }

    [Fact]
    public void Resolutor_RutasDeDataPower_EntreganLosRecursos()
    {
        foreach (var ruta in ResolutorRecursosLocales.Rutas)
        {
            using var flujo = (Stream)ResolutorRecursosLocales.Instancia.GetEntity(new Uri(ruta), null, typeof(Stream))!;
            Assert.True(flujo.Length > 0);
        }
    }

    [Fact]
    public void Adaptador_ConcatYRedeclaracion_MantienenLaSemantica()
    {
        const string xsl = """
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
              <xsl:template match="/">
                <r>
                  <xsl:variable name="x" select="1"/>
                  <a v="{$x}"/>
                  <xsl:if test="true()"><i v="{$x}"/></xsl:if>
                  <xsl:variable name="x" select="$x + 1"/>
                  <b v="{$x}"/>
                  <c v="{concat('k')}"/>
                  <d v="{$respaldo}"/>
                </r>
              </xsl:template>
            </xsl:stylesheet>
            """;

        var (adaptada, correcciones) = AdaptadorXslDataPower.Adaptar(XDocument.Parse(xsl), [new VariableRespaldoXsl("x.xsl", "respaldo", "'R'", "prueba")]);
        var salida = Transformar(adaptada);

        Assert.Equal(1, correcciones.ConcatUnArgumento);
        Assert.Equal("x", Assert.Single(correcciones.VariablesRedeclaradas));
        Assert.Equal("respaldo", Assert.Single(correcciones.VariablesRespaldo));
        Assert.Equal("""<r><a v="1" /><i v="1" /><b v="2" /><c v="k" /><d v="R" /></r>""", salida);
    }

    [Fact]
    public void Motor_ClaveDataPowerDesconocida_DevuelvePcpe2101()
    {
        var resultado = EjecutarSintetica("""<x v="{dp:variable('var://context/cpe/otraClave')}"/>""");

        Assert.Equal("PCPE-2101", resultado.Falla?.Codigo);
        Assert.Contains("var://context/cpe/otraClave", resultado.Falla!.Descripcion, StringComparison.Ordinal);
    }

    [Fact]
    public void Motor_PropiedadDeCatalogoDesconocida_DevuelvePcpe2102SinLanzar()
    {
        var resultado = EjecutarSintetica("""<x v="{pcpe:propiedadCatalogo('05', '1000', 'otra')}"/>""");

        Assert.Equal("PCPE-2102", resultado.Falla?.Codigo);
        Assert.Empty(resultado.Hallazgos);
    }

    [Fact]
    public async Task Validar_EnParalelo_ResultadosIdenticos()
    {
        using var servicios = new ServiciosPrueba();
        var (xml, referencia) = await GenerarAsync(servicios, "Gravada18");
        var invalido = Alterar(xml, r =>
        {
            r.Element(Cbc + "DocumentCurrencyCode")!.Value = "XYZ";
            r.Subtotal("1000").Element(Cbc + "TaxAmount")!.Value = "1.00";
        });
        var validador = servicios.Obtener<ValidadorSunat>();
        var esperado = Firma(validador.Validar(invalido, referencia));

        var resultados = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => Firma(validador.Validar(invalido, referencia)), TestContext.Current.CancellationToken)));

        Assert.NotEmpty(esperado);
        Assert.All(resultados, r => Assert.Equal(esperado, r));
    }

    [Fact]
    public async Task Validar_FacturaDeVeinteLineasEnCaliente_MenosDe300Ms()
    {
        using var servicios = new ServiciosPrueba();
        var datos = CasosTributarios.Todos["Gravada18"];
        var lineas = Enumerable.Range(1, 20).Select(i => CasosTributarios.Linea(i, 10m + i) with { Descripcion = $"PRODUCTO {i}" }).ToArray();
        var generado = await servicios.Obtener<PasheCPE.Mapeo.GeneradorInvoice>().GenerarAsync(datos.Emisor, CasosTributarios.Factura(lineas), TestContext.Current.CancellationToken);
        Assert.True(generado.Exito);
        var validador = servicios.Obtener<ValidadorSunat>();
        validador.Validar(generado.Xml!, generado.Referencia);

        var cronometro = Stopwatch.StartNew();
        for (var i = 0; i < 10; i++)
        {
            Assert.Empty(validador.Validar(generado.Xml!, generado.Referencia).Errores);
        }

        Assert.True(cronometro.Elapsed.TotalMilliseconds / 10 < 300, $"Promedio: {cronometro.Elapsed.TotalMilliseconds / 10:0.0} ms");
    }

    [Fact]
    public async Task Precalentador_CompilaTodoSinErrores()
    {
        using var servicios = new ServiciosPrueba();

        await servicios.Obtener<IPrecalentador>().PrecalentarAsync(TestContext.Current.CancellationToken);
    }

    private static string Firma(ResultadoValidacionSunat resultado) =>
        string.Join("|", resultado.Errores.Select(e => $"{e.Codigo}@{e.Ruta}").Concat(resultado.Observaciones.Select(o => o.Codigo)));

    private static ResultadoMotorXsl EjecutarSintetica(string cuerpo)
    {
        var xsl = $"""
            <xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:dp="http://www.datapower.com/extensions" xmlns:pcpe="urn:pashecpe:xsl">
              <xsl:template match="/">{cuerpo}</xsl:template>
            </xsl:stylesheet>
            """;
        var transformacion = new XslCompiledTransform();
        using (var lector = XmlReader.Create(new StringReader(xsl)))
        {
            transformacion.Load(lector);
        }

        using var servicios = new ServiciosPrueba();
        return servicios.Obtener<MotorXslSunat>().Ejecutar(transformacion, "sintetica.xsl", Encoding.UTF8.GetBytes("<Invoice/>"), Ejemplos.Referencia());
    }

    private static string Transformar(XDocument xsl)
    {
        var transformacion = new XslCompiledTransform();
        using (var lector = xsl.CreateReader())
        {
            transformacion.Load(lector);
        }

        var salida = new StringBuilder();
        using (var escritor = XmlWriter.Create(salida, new XmlWriterSettings { OmitXmlDeclaration = true }))
        using (var entrada = XmlReader.Create(new StringReader("<x/>")))
        {
            transformacion.Transform(entrada, escritor);
        }

        return salida.ToString();
    }
}
