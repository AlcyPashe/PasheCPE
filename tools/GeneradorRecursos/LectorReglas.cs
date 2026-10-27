using System.Text.RegularExpressions;

namespace PasheCPE.GeneradorRecursos;

/// <summary>Regla de validación de SUNAT (una fila de las hojas por documento).</summary>
internal sealed record ReglaSunat(
    string Documento,
    int Fila,
    string Numero,
    string Dato,
    string Nivel,
    string Condicion,
    string TipoLongitud,
    string Formato,
    string TagUbl,
    string Validacion,
    string Tipo,
    string? TipoAnterior,
    string Codigo,
    string Mensaje,
    string Listados,
    DateOnly? Vigencia,
    string Capa = "");

/// <summary>Fila normalizada de «Control de Cambios».</summary>
internal sealed record CambioSunat(
    int Fila,
    DateOnly? VersionExcel,
    IReadOnlyList<string> Codigos,
    string ErrorObservacion,
    string Campo,
    string Tipo,
    string Detalle,
    string Comprobante,
    IReadOnlyList<string> Documentos,
    string TextoVigencia,
    DateOnly? FechaVigencia);

/// <summary>
/// Lee las hojas de reglas (las columnas se ubican por el texto del encabezado, que cambia de letra entre hojas) y
/// «Control de Cambios». La vigencia de una regla es la fecha más reciente de los cambios que la mencionan.
/// </summary>
internal static partial class LectorReglas
{
    /// <summary>Hojas procesadas y documento que representan (TRD §3.2).</summary>
    public static readonly IReadOnlyList<(string Hoja, string Documento)> Hojas =
    [
        ("Factura2_0", "01"),
        ("Boleta2_0", "03"),
        ("NotaCredito2_0", "07"),
        ("NotaDebito2_0", "08"),
        ("Resumen Diario1_1", "RC"),
        ("Comunicación de Baja1_0", "RA"),
    ];

    /// <summary>Hojas del Excel GRE (TRD §3.2) y documento que representan.</summary>
    public static readonly IReadOnlyList<(string Hoja, string Documento)> HojasGre =
    [
        ("Guía-Remitente2_0", "09"),
        ("Guía-Transportista2_0", "31"),
    ];

    public static IReadOnlyList<CambioSunat> LeerCambios(HojaExcel hoja)
    {
        var encabezado = hoja.Filas.First(f => f.Celdas.Values.Any(v => Texto.Clave(v) == "FECHA VIGENCIA"));
        string Columna(string clave) => encabezado.Celdas.First(c => Texto.Clave(c.Value).StartsWith(clave, StringComparison.Ordinal)).Key;
        var (version, codigos, campo, tipo, detalle, comprobante, vigencia) = (
            Columna("VERSION EXCEL"), Columna("ERROR/OBSERVACION"), Columna("CAMPO"), Columna("TIPO"),
            Columna("DETALLE"), ColumnaDocumento(), Columna("FECHA VIGENCIA"));
        // El Excel CPE llama «COMPROBANTE» a la columna que el Excel GRE llama «DOCUMENTO».
        string ColumnaDocumento() => encabezado.Celdas.First(c =>
            Texto.Clave(c.Value).StartsWith("COMPROBANTE", StringComparison.Ordinal) || Texto.Clave(c.Value).StartsWith("DOCUMENTO", StringComparison.Ordinal)).Key;

        var cambios = new List<CambioSunat>();
        foreach (var fila in hoja.Filas.Where(f => f.Numero > encabezado.Numero))
        {
            var textoCodigos = Texto.UnaLinea(fila[codigos]);
            var textoComprobante = Texto.UnaLinea(fila[comprobante]);
            if (fila.Celdas.Values.All(v => Texto.UnaLinea(v).Length == 0))
            {
                continue;
            }

            cambios.Add(new CambioSunat(
                fila.Numero,
                FechasExcel.Ultima(fila[version]),
                CodigoError().IsMatch(textoCodigos) ? [.. CuatroDigitos().Matches(textoCodigos).Select(m => m.Value).Distinct(StringComparer.Ordinal)] : [],
                textoCodigos,
                Texto.Multilinea(fila[campo]),
                Texto.UnaLinea(fila[tipo]),
                Texto.Multilinea(fila[detalle]),
                textoComprobante,
                Documentos(textoComprobante),
                Texto.UnaLinea(fila[vigencia]),
                FechasExcel.Ultima(fila[vigencia])));
        }

        return cambios;
    }

    public static IReadOnlyList<ReglaSunat> Leer(HojaExcel hoja, string documento, IReadOnlyList<CambioSunat> cambios, out int filasSinCodigo)
    {
        filasSinCodigo = 0;
        var encabezado = hoja.Filas.First(f => f.Celdas.Values.Any(v => Texto.Clave(v) == "TAG UBL"));
        var columnas = encabezado.Celdas.ToDictionary(c => Campo(Texto.Clave(c.Value)), c => c.Key, StringComparer.Ordinal);
        string? Valor(FilaExcel fila, string campo) => columnas.TryGetValue(campo, out var columna) ? fila[columna] : null;

        var reglas = new List<ReglaSunat>();
        string numero = string.Empty, dato = string.Empty, nivel = string.Empty;
        foreach (var fila in hoja.Filas.Where(f => f.Numero > encabezado.Numero))
        {
            // Las celdas combinadas ya traen el valor del grupo; el arrastre cubre filas que no están combinadas.
            numero = Texto.UnaLinea(Valor(fila, "numero")) is { Length: > 0 } n ? n : numero;
            dato = Texto.UnaLinea(Valor(fila, "dato")) is { Length: > 0 } d ? d : dato;
            nivel = Texto.UnaLinea(Valor(fila, "nivel")) is { Length: > 0 } v ? v : nivel;

            if (Texto.CodigoRetorno(Valor(fila, "codigo")) is not { } codigo)
            {
                if (Texto.UnaLinea(Valor(fila, "validacion")) is { Length: > 0 } validacionSinCodigo && validacionSinCodigo != "-")
                {
                    filasSinCodigo++;
                }

                continue;
            }

            var (tipo, tipoAnterior) = Tipo(Valor(fila, "tipo"));
            reglas.Add(new ReglaSunat(
                documento,
                fila.Numero,
                numero,
                dato,
                nivel,
                Texto.UnaLinea(Valor(fila, "condicion")),
                Texto.UnaLinea(Valor(fila, "tipoLongitud")),
                Texto.Multilinea(Valor(fila, "formato")),
                Texto.Multilinea(Valor(fila, "tagUbl")),
                Texto.Multilinea(Valor(fila, "validacion")),
                tipo,
                tipoAnterior,
                codigo,
                Texto.UnaLinea(Valor(fila, "mensaje")),
                Texto.UnaLinea(Valor(fila, "listados")) is "-" ? string.Empty : Texto.UnaLinea(Valor(fila, "listados")),
                cambios
                    .Where(c => c.FechaVigencia is not null && c.Documentos.Contains(documento) && c.Codigos.Contains(codigo))
                    .Max(c => c.FechaVigencia),
                Texto.Clave(Valor(fila, "capa"))));
        }

        return reglas;
    }

    private static string Campo(string encabezado) => encabezado switch
    {
        "N°" or "N.°" or "NO." => "numero",
        "DATO" => "dato",
        "NIVEL" => "nivel",
        "CONDICION INFORMATICA" => "condicion",
        "TIPO Y LONGITUD" => "tipoLongitud",
        "TAG UBL" => "tagUbl",
        "USO DEL CAMPO" => "uso",
        "TIPO DE RETORNO" => "tipo",
        "MENSAJE DE RETORNO" => "mensaje",
        "LISTADOS" => "listados",
        "REST O XSL" => "capa",
        _ when encabezado.StartsWith("FORMATO", StringComparison.Ordinal) => "formato",
        _ when encabezado.StartsWith("VALIDACION", StringComparison.Ordinal) => "validacion",
        _ when encabezado.Contains("CODIGO", StringComparison.Ordinal) && encabezado.Contains("RETORNO", StringComparison.Ordinal) => "codigo",
        _ => "otro:" + encabezado,
    };

    /// <summary>
    /// Tipo de retorno. «OBSERV ERROR» marca una regla en transición (pasa de observación a error en la fecha de su
    /// vigencia, p. ej. el 3496 desde el 01/01/2027): el tipo es el último y el anterior queda en <c>tipoAnterior</c>.
    /// </summary>
    private static (string Tipo, string? TipoAnterior) Tipo(string? valor)
    {
        var tipos = Texto.Clave(valor).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(TipoSimple).ToList();
        return tipos switch
        {
            [] => (string.Empty, null),
            [var unico] => (unico, null),
            [var anterior, .., var actual] => (actual, anterior),
        };
    }

    private static string TipoSimple(string valor) => valor switch
    {
        "ERROR" => "error",
        "OBSERV" or "OBSERVACION" => "observacion",
        _ => valor.ToLowerInvariant(),
    };

    private static List<string> Documentos(string comprobante)
    {
        var clave = Texto.Clave(comprobante);
        var documentos = new List<string>();
        if (clave.Contains("FAC", StringComparison.Ordinal))
        {
            documentos.Add("01");
        }

        if (clave.Contains("BOL", StringComparison.Ordinal))
        {
            documentos.Add("03");
        }

        if (Palabra("NC").IsMatch(clave) || clave.Contains("NOTA DE CREDITO", StringComparison.Ordinal))
        {
            documentos.Add("07");
        }

        if (Palabra("ND").IsMatch(clave) || clave.Contains("NOTA DE DEBITO", StringComparison.Ordinal))
        {
            documentos.Add("08");
        }

        if (Palabra("RC").IsMatch(clave) || clave.Contains("RESUMEN DIARIO", StringComparison.Ordinal))
        {
            documentos.Add("RC");
        }

        if (Palabra("RA").IsMatch(clave) || clave.Contains("COMUNICACION DE BAJA", StringComparison.Ordinal))
        {
            documentos.Add("RA");
        }

        // Excel GRE: «GRE-R», «GRE-T» o «GRE-R y GRE-T».
        if (clave.Contains("GRE-R", StringComparison.Ordinal))
        {
            documentos.Add("09");
        }

        if (clave.Contains("GRE-T", StringComparison.Ordinal))
        {
            documentos.Add("31");
        }

        return documentos;
    }

    private static Regex Palabra(string palabra) => new($@"\b{palabra}\b", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"ERR|OBS")]
    private static partial Regex CodigoError();

    [GeneratedRegex(@"\b\d{4}\b")]
    private static partial Regex CuatroDigitos();
}
