using System.Text;
using System.Text.Json;
using PasheCPE.GeneradorRecursos;

namespace GeneradorRecursos.Tests;

/// <summary>Ejecuta el generador dos veces sobre el Excel real del repositorio (solo lectura).</summary>
public sealed class EjecucionesGenerador
{
    public EjecucionesGenerador()
    {
        Raiz = Generador.BuscarRaiz(AppContext.BaseDirectory);
        Primera = Generador.Ejecutar(Raiz);
        Segunda = Generador.Ejecutar(Raiz);
    }

    public string Raiz { get; }

    internal ResultadoGenerador Primera { get; }

    internal ResultadoGenerador Segunda { get; }
}

public sealed class GeneradorTests(EjecucionesGenerador ejecuciones) : IClassFixture<EjecucionesGenerador>
{
    private static readonly string[] _tiposRegla = ["error", "observacion"];

    [Fact]
    public void Ejecutar_DosVeces_ProduceLosMismosBytes()
    {
        Assert.Equal(ejecuciones.Primera.Archivos.Keys, ejecuciones.Segunda.Archivos.Keys);
        Assert.All(ejecuciones.Primera.Archivos, archivo => Assert.Equal(archivo.Value, ejecuciones.Segunda.Archivos[archivo.Key]));
    }

    [Fact]
    public void Ejecutar_CoincideConLosRecursosDelRepositorio()
    {
        var desactualizados = ejecuciones.Primera.Archivos
            .Where(a => !File.Exists(Path.Combine(ejecuciones.Raiz, a.Key)) || !File.ReadAllBytes(Path.Combine(ejecuciones.Raiz, a.Key)).AsSpan().SequenceEqual(a.Value))
            .Select(a => a.Key)
            .ToList();

        Assert.True(desactualizados.Count == 0, $"Ejecute `dotnet run --project tools/GeneradorRecursos`. Desactualizados: {string.Join(", ", desactualizados)}");
    }

    [Fact]
    public void Ejecutar_ConteosDelExcel_26_08_2026()
    {
        var resultado = ejecuciones.Primera;

        Assert.Equal(45, resultado.Catalogos);
        Assert.Equal(2077, resultado.Errores);
        Assert.Equal(562, resultado.Cambios);
        Assert.Equal(0, resultado.ConstantesNuevas);
        Assert.Equal(["01", "03", "07", "08", "RA", "RC"], resultado.ReglasPorDocumento.Keys.Order(StringComparer.Ordinal));
        Assert.All(resultado.ReglasPorDocumento.Values, cantidad => Assert.True(cantidad > 40));
        Assert.Contains(resultado.Resumen, linea => linea.StartsWith("Catálogos: 45", StringComparison.Ordinal));
    }

    [Fact]
    public void ReglasCpe_CadaReglaTieneCodigoYTipo()
    {
        using var json = Json("src/PasheCPE/Recursos/reglas-cpe.json");
        var reglas = json.RootElement.GetProperty("reglas").EnumerateArray().ToList();

        Assert.All(reglas, regla =>
        {
            Assert.Matches(@"^\d{4}$", regla.GetProperty("codigo").GetString());
            Assert.Contains(regla.GetProperty("tipo").GetString(), _tiposRegla);
        });
    }

    [Fact]
    public void ReglasCpe_CodigoProductoSunat_PasaDeObservacionAErrorEn2027()
    {
        using var json = Json("src/PasheCPE/Recursos/reglas-cpe.json");
        var reglas3496 = json.RootElement.GetProperty("reglas").EnumerateArray()
            .Where(r => r.GetProperty("codigo").GetString() == "3496")
            .ToList();

        Assert.Equal(["01", "03", "07", "08"], reglas3496.Select(r => r.GetProperty("documento").GetString()).Distinct().Order(StringComparer.Ordinal));
        Assert.All(reglas3496, regla =>
        {
            Assert.Equal("error", regla.GetProperty("tipo").GetString());
            Assert.Equal("observacion", regla.GetProperty("tipoAnterior").GetString());
            Assert.Equal("2027-01-01", regla.GetProperty("vigencia").GetString());
        });
    }

    [Fact]
    public void ReglasCpe_ReglaDeLaNotaDeCredito2027_TieneVigencia()
    {
        using var json = Json("src/PasheCPE/Recursos/reglas-cpe.json");
        var regla3261 = json.RootElement.GetProperty("reglas").EnumerateArray()
            .First(r => r.GetProperty("documento").GetString() == "07" && r.GetProperty("codigo").GetString() == "3261");

        Assert.Equal("2027-01-01", regla3261.GetProperty("vigencia").GetString());
    }

    [Fact]
    public void Catalogos_LosDeEnlaceSeValidanPorFormato()
    {
        using var json = Json("src/PasheCPE/Recursos/catalogos.json");
        var catalogos = json.RootElement.GetProperty("catalogos");

        foreach (var numero in new[] { "02", "03", "04", "13", "25" })
        {
            Assert.True(catalogos.GetProperty(numero).GetProperty("soloFormato").GetBoolean());
            Assert.Equal(0, catalogos.GetProperty(numero).GetProperty("items").GetArrayLength());
        }
    }

    [Fact]
    public void ErroresSunat_SoloLosCuradosSonTransitorios()
    {
        using var json = Json("src/PasheCPE/Recursos/errores-sunat.json");
        var transitorios = json.RootElement.GetProperty("errores").EnumerateArray()
            .Where(e => e.GetProperty("transitorio").GetBoolean())
            .Select(e => e.GetProperty("codigo").GetString())
            .ToList();

        Assert.Equal(18, transitorios.Count);
        Assert.Contains("0100", transitorios);
        Assert.DoesNotContain("0111", transitorios);
        // 0250 y 0252 apuntan al ZIP enviado (reenviarlo no lo arregla); 0140 es «en proceso», no transitorio.
        Assert.DoesNotContain("0250", transitorios);
        Assert.DoesNotContain("0252", transitorios);
        Assert.DoesNotContain("0140", transitorios);
        var enProceso = json.RootElement.GetProperty("errores").EnumerateArray()
            .Where(e => e.TryGetProperty("enProceso", out var valor) && valor.GetBoolean())
            .Select(e => e.GetProperty("codigo").GetString());
        Assert.Equal(["0140"], enProceso);
    }

    [Fact]
    public void ErroresSunat_TransitoriosConGrupoDeDisponibilidadOConstancia()
    {
        using var json = Json("src/PasheCPE/Recursos/errores-sunat.json");
        var grupos = json.RootElement.GetProperty("errores").EnumerateArray()
            .Where(e => e.GetProperty("transitorio").GetBoolean())
            .ToDictionary(e => e.GetProperty("codigo").GetString()!, e => e.GetProperty("grupoTransitorio").GetString());

        // TRD §14: disponibilidad (SUNAT no recibió el documento) frente a constancia (pudo haberlo registrado).
        Assert.Equal(["0100", "0109", "0138"], grupos.Where(g => g.Value == "disponibilidad").Select(g => g.Key).Order(StringComparer.Ordinal));
        Assert.All(grupos.Values, g => Assert.True(g is "disponibilidad" or "constancia", g));
        Assert.Equal("constancia", grupos["0125"]);
        Assert.DoesNotContain(json.RootElement.GetProperty("errores").EnumerateArray(), e => !e.GetProperty("transitorio").GetBoolean() && e.TryGetProperty("grupoTransitorio", out _));
    }

    [Fact]
    public void Constantes_UnidadesDeMedidaCuradas()
    {
        var codigo = Encoding.UTF8.GetString(ejecuciones.Primera.Archivos[Generador.ArchivoConstantes]);

        Assert.Contains("public static class UnidadMedida", codigo, StringComparison.Ordinal);
        Assert.Contains("public const string NumeroCatalogo = \"03\";", codigo, StringComparison.Ordinal);
        foreach (var (nombre, unidad) in new[] { ("Unidad", "NIU"), ("Servicio", "ZZ"), ("Kilogramo", "KGM"), ("Litro", "LTR"), ("Metro", "MTR"), ("Caja", "BX"), ("Galon", "GLL") })
        {
            Assert.Contains($"public const string {nombre} = \"{unidad}\";", codigo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Constantes_UnidadQueNoCumpleElFormato_Falla()
    {
        var catalogo = new Catalogo("03", "Unidades", [], "^[A-Z0-9]{1,3}$", [], null, null, []);
        var unidades = new UnidadesCuradas("03", "UnidadMedida", "Prueba", [new UnidadCurada("niu", "Unidad", "Unidad")]);

        Assert.Throws<InvalidDataException>(() => GeneradorConstantes.Generar([catalogo], [], unidades, "prueba.xlsx"));
    }

    [Fact]
    public void Constantes_ConservanElNombreCurado()
    {
        var nombres = File.ReadAllText(Path.Combine(ejecuciones.Raiz, DatosCurados.ArchivoNombres))
            .Replace("\"nombre\": \"VentaInterna\"", "\"nombre\": \"VentaInternaRenombrada\"", StringComparison.Ordinal);

        var resultado = Generador.Ejecutar(ejecuciones.Raiz, nombres);
        var codigo = Encoding.UTF8.GetString(resultado.Archivos[Generador.ArchivoConstantes]);

        Assert.Contains("public const string VentaInternaRenombrada = \"0101\";", codigo, StringComparison.Ordinal);
        Assert.Equal(0, resultado.ConstantesNuevas);
    }

    [Fact]
    public void Constantes_CodigoNuevo_RecibeNombreDerivadoDeLaDescripcion()
    {
        var nombres = File.ReadAllText(Path.Combine(ejecuciones.Raiz, DatosCurados.ArchivoNombres))
            .Replace("{\n          \"codigo\": \"13\",\n          \"nombre\": \"Penalidades\"\n        }", "{\n          \"codigo\": \"99\",\n          \"nombre\": \"Inexistente\"\n        }", StringComparison.Ordinal);

        var resultado = Generador.Ejecutar(ejecuciones.Raiz, nombres);
        var codigo = Encoding.UTF8.GetString(resultado.Archivos[Generador.ArchivoConstantes]);

        Assert.Equal(1, resultado.ConstantesNuevas);
        Assert.Contains("public const string Penalidades = \"13\";", codigo, StringComparison.Ordinal);
        Assert.DoesNotContain("Inexistente", codigo, StringComparison.Ordinal);
    }

    private JsonDocument Json(string ruta) => JsonDocument.Parse(ejecuciones.Primera.Archivos[ruta]);
}

public sealed class UtilidadesGeneradorTests
{
    [Theory]
    [InlineData("46388", "2027-01-01")]
    [InlineData("46235", "2026-08-01")]
    [InlineData("01/08/2026 01/01/2027", "2027-01-01")]
    [InlineData("30/06/2018", "2018-06-30")]
    public void FechasExcel_Ultima_InterpretaSerialYTexto(string valor, string esperado) =>
        Assert.Equal(DateOnly.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), FechasExcel.Ultima(valor));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin fecha")]
    public void FechasExcel_Ultima_SinFecha_EsNull(string valor) => Assert.Null(FechasExcel.Ultima(valor));

    [Theory]
    [InlineData("Gravado - Operación Onerosa", "10", "GravadoOperacionOnerosa")]
    [InlineData("DOC.TRIB.NO.DOM.SIN.RUC", "0", "DocTribNoDomSinRuc")]
    [InlineData("Leyenda “BIENES TRANSFERIDOS EN LA AMAZONÍA REGIÓN SELVA”", "2001", "LeyendaBienesTransferidosEnLaAmazonia")]
    [InlineData("Año ñandú", "1", "AnoNandu")]
    [InlineData("123 código", "1", "C123Codigo")]
    [InlineData("—", "25.1", "Codigo251")]
    public void Constantes_Derivar_PascalCaseSinTildes(string descripcion, string codigo, string esperado) =>
        Assert.Equal(esperado, GeneradorConstantes.Derivar(descripcion, codigo));

    [Theory]
    [InlineData("Código internacional", "codigo-internacional")]
    [InlineData("Porcentaje %", "porcentaje")]
    [InlineData("N.°", "n")]
    [InlineData("Id", "col-id")]
    public void Escritura_NombreAtributo_EsValidoEnXml(string encabezado, string esperado) =>
        Assert.Equal(esperado, Escritura.NombreAtributo(encabezado));
}
