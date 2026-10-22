using System.Text.Json;

namespace PasheCPE.GeneradorRecursos;

/// <summary>
/// Datos que el Excel no trae de forma estructurada y se mantienen a mano en <c>tools/GeneradorRecursos/Datos</c>.
/// Cada entrada cita su origen (fila de «Control de Cambios» o norma).
/// </summary>
internal sealed record DatosCurados(
    IReadOnlyList<VigenciaCurada> Vigencias,
    IReadOnlyList<FormatoCurado> Formatos,
    IReadOnlyList<TransitorioCurado> Transitorios,
    IReadOnlyList<ClaseConstantes> Clases)
{
    public const string Carpeta = "tools/GeneradorRecursos/Datos";
    public const string ArchivoNombres = Carpeta + "/nombres-constantes.json";

    private static readonly JsonSerializerOptions _opciones = new(JsonSerializerDefaults.Web);

    public static DatosCurados Cargar(string raiz, string? nombresConstantes = null)
    {
        var carpeta = Path.Combine(raiz, Carpeta);
        return new DatosCurados(
            Leer<ArchivoVigencias>(Path.Combine(carpeta, "vigencias-catalogos.json")).Vigencias,
            Leer<ArchivoFormatos>(Path.Combine(carpeta, "formatos-catalogos.json")).Formatos,
            Leer<ArchivoTransitorios>(Path.Combine(carpeta, "errores-transitorios.json")).Transitorios,
            (nombresConstantes is null
                ? Leer<ArchivoNombresConstantes>(Path.Combine(raiz, ArchivoNombres))
                : Deserializar<ArchivoNombresConstantes>(nombresConstantes, "nombres de constantes")).Clases);
    }

    private static T Leer<T>(string ruta) => Deserializar<T>(File.ReadAllText(ruta), ruta);

    private static T Deserializar<T>(string json, string origen) =>
        JsonSerializer.Deserialize<T>(json, _opciones) ?? throw new InvalidDataException($"Archivo vacío: {origen}.");

    private sealed record ArchivoVigencias(IReadOnlyList<VigenciaCurada> Vigencias);

    private sealed record ArchivoFormatos(IReadOnlyList<FormatoCurado> Formatos);

    private sealed record ArchivoTransitorios(IReadOnlyList<TransitorioCurado> Transitorios);
}

/// <summary>Vigencia de un código (o de todo el catálogo si <see cref="Codigo"/> es null).</summary>
internal sealed record VigenciaCurada(string Catalogo, string? Codigo, DateOnly? Desde, DateOnly? Hasta, string Fuente);

/// <summary>Catálogo que el Excel publica solo como enlace: se valida por formato.</summary>
internal sealed record FormatoCurado(string Catalogo, string Formato, string Fuente);

/// <summary>Código de error de servicio que indica una falla temporal (se puede reintentar).</summary>
internal sealed record TransitorioCurado(string Codigo, string Motivo);

/// <summary>Archivo de nombres estables de las constantes generadas.</summary>
internal sealed record ArchivoNombresConstantes(IReadOnlyList<ClaseConstantes> Clases);

/// <summary>Clase de constantes de un catálogo, con el nombre fijado para cada código.</summary>
internal sealed record ClaseConstantes(string Catalogo, string Clase, IReadOnlyList<NombreConstante> Constantes);

/// <summary>Nombre de la constante de un código.</summary>
internal sealed record NombreConstante(string Codigo, string Nombre);
