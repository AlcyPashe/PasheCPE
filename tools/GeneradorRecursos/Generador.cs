using System.Security.Cryptography;
using System.Text.Json;

namespace PasheCPE.GeneradorRecursos;

/// <summary>Salida del generador: archivos (ruta relativa a la raíz del repositorio → bytes) y conteos.</summary>
internal sealed record ResultadoGenerador(
    IReadOnlyDictionary<string, byte[]> Archivos,
    IReadOnlyList<string> Resumen,
    int Catalogos,
    int Errores,
    IReadOnlyDictionary<string, int> ReglasPorDocumento,
    int Cambios,
    int ConstantesNuevas);

/// <summary>
/// Excel de reglas de SUNAT (TRD §3.2) → <c>catalogos.json</c>, <c>catalogos/cat_XX.xml</c>,
/// <c>errores-sunat.json</c>, <c>reglas-cpe.json</c> y <c>CodigosSunat.g.cs</c>. Todo se calcula en memoria;
/// <c>Program</c> escribe en disco solo lo que cambió.
/// </summary>
internal static class Generador
{
    public const string CarpetaRecursos = "src/PasheCPE/Recursos";
    public const string ArchivoConstantes = "src/PasheCPE/Catalogos/CodigosSunat.g.cs";

    public static ResultadoGenerador Ejecutar(string raiz, string? nombresConstantes = null)
    {
        var rutaExcel = BuscarExcel(raiz);
        var archivoExcel = Path.GetRelativePath(raiz, rutaExcel).Replace('\\', '/');
        var fuente = new Fuente(archivoExcel, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(rutaExcel))));
        var datos = DatosCurados.Cargar(raiz, nombresConstantes);

        using var libro = new LibroExcel(rutaExcel);
        var catalogos = LectorCatalogos.Leer(libro.Hoja("Catálogos"), datos);
        var duplicadosCatalogo = catalogos.GroupBy(c => c.Numero, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicadosCatalogo.Count > 0)
        {
            throw new InvalidDataException($"Catálogos repetidos en el Excel: {string.Join(", ", duplicadosCatalogo)}.");
        }

        var errores = LectorErrores.Leer(libro.Hoja("CódigosRetorno"), libro.Hoja("General "), datos, out var erroresDuplicados);
        var cambios = LectorReglas.LeerCambios(libro.Hoja("Control de Cambios"));
        var reglas = new List<ReglaSunat>();
        var resumen = new List<string>
        {
            $"Excel: {archivoExcel} (SHA-256 {fuente.Sha256})",
            $"Catálogos: {catalogos.Count} (códigos: {catalogos.Sum(c => c.Items.Count)}; solo formato: {catalogos.Count(c => c.Formato is not null)}; con vigencia: {catalogos.Sum(c => c.Items.Count(i => i.Desde is not null || i.Hasta is not null)) + catalogos.Count(c => c.Desde is not null || c.Hasta is not null)})",
            $"CódigosRetorno + General: {errores.Count} códigos (servicio: {errores.Count(e => e.Tipo == "servicio")}; rechazo: {errores.Count(e => e.Tipo == "rechazo")}; observación: {errores.Count(e => e.Tipo == "observacion")}; transitorios: {errores.Count(e => e.Transitorio)}; en proceso: {errores.Count(e => e.EnProceso)}; repetidos ignorados: {erroresDuplicados})",
        };

        var reglasPorDocumento = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (hoja, documento) in LectorReglas.Hojas)
        {
            var reglasHoja = LectorReglas.Leer(libro.Hoja(hoja, aplicarCombinadas: true), documento, cambios, out var sinCodigo);
            reglas.AddRange(reglasHoja);
            reglasPorDocumento[documento] = reglasHoja.Count;
            resumen.Add($"{hoja}: {reglasHoja.Count} reglas (con vigencia: {reglasHoja.Count(r => r.Vigencia is not null)}; validaciones sin código: {sinCodigo})");
        }

        resumen.Add($"Control de Cambios: {cambios.Count} filas (con fecha de vigencia: {cambios.Count(c => c.FechaVigencia is not null)})");

        var (codigoConstantes, nombres, nuevas, retiradas) = GeneradorConstantes.Generar(catalogos, datos.Clases, datos.Unidades, archivoExcel);
        resumen.Add($"Constantes: {nombres.Count} clases, {nombres.Sum(n => n.Constantes.Count)} constantes (nuevas: {nuevas}; retiradas: {retiradas}); {datos.Unidades.Clase}: {datos.Unidades.Unidades.Count} unidades curadas");

        var archivos = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [$"{CarpetaRecursos}/catalogos.json"] = CatalogosJson(fuente, catalogos),
            [$"{CarpetaRecursos}/errores-sunat.json"] = ErroresJson(fuente, errores),
            [$"{CarpetaRecursos}/reglas-cpe.json"] = ReglasJson(fuente, reglas, cambios),
            [ArchivoConstantes] = Escritura.Utf8(codigoConstantes),
            [DatosCurados.ArchivoNombres] = NombresJson(nombres),
        };
        foreach (var catalogo in catalogos)
        {
            archivos[$"{CarpetaRecursos}/catalogos/cat_{catalogo.Numero}.xml"] = Escritura.CatalogoXml(catalogo);
        }

        return new ResultadoGenerador(archivos, resumen, catalogos.Count, errores.Count, reglasPorDocumento, cambios.Count, nuevas);
    }

    /// <summary>El Excel de reglas CPE más reciente de <c>recursos/VALIDACIONES</c> (la fecha va en el nombre).</summary>
    public static string BuscarExcel(string raiz)
    {
        var carpeta = Path.Combine(raiz, "recursos", "VALIDACIONES");
        return Directory.GetFiles(carpeta, "ReglasValidacion-CPE-*.xlsx").Order(StringComparer.Ordinal).LastOrDefault()
            ?? throw new FileNotFoundException($"No se encontró ReglasValidacion-CPE-*.xlsx en {carpeta}.");
    }

    /// <summary>Raíz del repositorio: la primera carpeta, hacia arriba, que contiene <c>PasheCPE.slnx</c>.</summary>
    public static string BuscarRaiz(string desde)
    {
        for (var carpeta = new DirectoryInfo(desde); carpeta is not null; carpeta = carpeta.Parent)
        {
            if (File.Exists(Path.Combine(carpeta.FullName, "PasheCPE.slnx")))
            {
                return carpeta.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No se encontró PasheCPE.slnx desde {desde}.");
    }

    private static byte[] CatalogosJson(Fuente fuente, IReadOnlyList<Catalogo> catalogos) => Escritura.Json(json =>
    {
        json.WriteStartObject();
        Escritura.Fuente(json, fuente);
        json.WriteStartObject("catalogos");
        foreach (var catalogo in catalogos)
        {
            json.WriteStartObject(catalogo.Numero);
            json.WriteString("nombre", catalogo.Nombre);
            Lista(json, "columnas", catalogo.Columnas);
            if (catalogo.Formato is not null)
            {
                json.WriteBoolean("soloFormato", true);
                json.WriteString("formato", catalogo.Formato);
            }

            if (catalogo.Enlace.Count > 0)
            {
                Lista(json, "enlace", catalogo.Enlace);
            }

            Escritura.Fecha(json, "desde", catalogo.Desde);
            Escritura.Fecha(json, "hasta", catalogo.Hasta);
            json.WriteStartArray("items");
            foreach (var item in catalogo.Items)
            {
                json.WriteStartObject();
                json.WriteString("codigo", item.Codigo);
                json.WriteString("descripcion", item.Descripcion);
                if (item.Extras.Count > 0)
                {
                    json.WriteStartObject("extras");
                    foreach (var (columna, valor) in item.Extras)
                    {
                        json.WriteString(columna, valor);
                    }

                    json.WriteEndObject();
                }

                Escritura.Fecha(json, "desde", item.Desde);
                Escritura.Fecha(json, "hasta", item.Hasta);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndObject();
        json.WriteEndObject();
    });

    private static byte[] ErroresJson(Fuente fuente, IReadOnlyList<ErrorSunat> errores) => Escritura.Json(json =>
    {
        json.WriteStartObject();
        Escritura.Fuente(json, fuente);
        json.WriteStartArray("errores");
        foreach (var error in errores)
        {
            json.WriteStartObject();
            json.WriteString("codigo", error.Codigo);
            json.WriteString("descripcion", error.Descripcion);
            json.WriteString("tipo", error.Tipo);
            json.WriteBoolean("transitorio", error.Transitorio);
            if (error.EnProceso)
            {
                json.WriteBoolean("enProceso", true);
            }
            if (error.Condiciones.Count > 0)
            {
                Lista(json, "condiciones", error.Condiciones);
            }

            Escritura.TextoOpcional(json, "listados", error.Listados);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    });

    private static byte[] ReglasJson(Fuente fuente, IReadOnlyList<ReglaSunat> reglas, IReadOnlyList<CambioSunat> cambios) => Escritura.Json(json =>
    {
        json.WriteStartObject();
        Escritura.Fuente(json, fuente);
        json.WriteStartArray("reglas");
        foreach (var regla in reglas)
        {
            json.WriteStartObject();
            json.WriteString("documento", regla.Documento);
            json.WriteNumber("fila", regla.Fila);
            Escritura.TextoOpcional(json, "numero", regla.Numero);
            Escritura.TextoOpcional(json, "dato", regla.Dato);
            Escritura.TextoOpcional(json, "nivel", regla.Nivel);
            Escritura.TextoOpcional(json, "condicion", regla.Condicion);
            Escritura.TextoOpcional(json, "tipoLongitud", regla.TipoLongitud);
            Escritura.TextoOpcional(json, "formato", regla.Formato);
            Escritura.TextoOpcional(json, "tagUbl", regla.TagUbl);
            Escritura.TextoOpcional(json, "validacion", regla.Validacion);
            json.WriteString("tipo", regla.Tipo);
            Escritura.TextoOpcional(json, "tipoAnterior", regla.TipoAnterior);
            json.WriteString("codigo", regla.Codigo);
            Escritura.TextoOpcional(json, "mensaje", regla.Mensaje);
            Escritura.TextoOpcional(json, "listados", regla.Listados);
            Escritura.Fecha(json, "vigencia", regla.Vigencia);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("controlCambios");
        foreach (var cambio in cambios)
        {
            json.WriteStartObject();
            json.WriteNumber("fila", cambio.Fila);
            Escritura.Fecha(json, "versionExcel", cambio.VersionExcel);
            if (cambio.Codigos.Count > 0)
            {
                Lista(json, "codigos", cambio.Codigos);
            }

            Escritura.TextoOpcional(json, "errorObservacion", cambio.ErrorObservacion);
            Escritura.TextoOpcional(json, "campo", cambio.Campo);
            Escritura.TextoOpcional(json, "tipo", cambio.Tipo);
            Escritura.TextoOpcional(json, "detalle", cambio.Detalle);
            Escritura.TextoOpcional(json, "comprobante", cambio.Comprobante);
            if (cambio.Documentos.Count > 0)
            {
                Lista(json, "documentos", cambio.Documentos);
            }

            Escritura.TextoOpcional(json, "textoVigencia", cambio.TextoVigencia);
            Escritura.Fecha(json, "fechaVigencia", cambio.FechaVigencia);
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    });

    private static byte[] NombresJson(IReadOnlyList<ClaseConstantes> clases) => Escritura.Json(json =>
    {
        json.WriteStartObject();
        json.WriteStartArray("clases");
        foreach (var clase in clases)
        {
            json.WriteStartObject();
            json.WriteString("catalogo", clase.Catalogo);
            json.WriteString("clase", clase.Clase);
            json.WriteStartArray("constantes");
            foreach (var constante in clase.Constantes)
            {
                json.WriteStartObject();
                json.WriteString("codigo", constante.Codigo);
                json.WriteString("nombre", constante.Nombre);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
    });

    private static void Lista(Utf8JsonWriter json, string nombre, IEnumerable<string> valores)
    {
        json.WriteStartArray(nombre);
        foreach (var valor in valores)
        {
            json.WriteStringValue(valor);
        }

        json.WriteEndArray();
    }
}
