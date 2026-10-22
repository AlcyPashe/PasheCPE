namespace PasheCPE.GeneradorRecursos;

/// <summary>Código de retorno de SUNAT con su clasificación.</summary>
internal sealed record ErrorSunat(
    string Codigo,
    string Descripcion,
    string Tipo,
    bool Transitorio,
    IReadOnlyList<string> Condiciones,
    string? Listados);

/// <summary>
/// Lee «CódigosRetorno» (descripción oficial) y «General » (validaciones del servicio). El tipo sale del rango del
/// código: menos de 2000 = servicio (fault), 2000–3999 = rechazo, 4000 o más = observación.
/// </summary>
internal static class LectorErrores
{
    public static IReadOnlyList<ErrorSunat> Leer(HojaExcel codigosRetorno, HojaExcel general, DatosCurados datos, out int duplicados)
    {
        duplicados = 0;
        var descripciones = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fila in codigosRetorno.Filas)
        {
            if (Texto.CodigoRetorno(fila["A"]) is not { } codigo)
            {
                continue;
            }

            if (!descripciones.TryAdd(codigo, Texto.UnaLinea(fila["B"])))
            {
                duplicados++;
            }
        }

        var condiciones = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var listados = new Dictionary<string, string>(StringComparer.Ordinal);
        var columnas = Columnas(general);
        foreach (var fila in general.Filas)
        {
            if (Texto.CodigoRetorno(fila[columnas.Codigo]) is not { } codigo)
            {
                continue;
            }

            descripciones.TryAdd(codigo, Texto.UnaLinea(fila[columnas.Descripcion]));
            var condicion = Texto.UnaLinea(fila[columnas.Condicion]);
            if (condicion.Length > 0)
            {
                var lista = condiciones.TryGetValue(codigo, out var existente) ? existente : condiciones[codigo] = [];
                if (!lista.Contains(condicion, StringComparer.Ordinal))
                {
                    lista.Add(condicion);
                }
            }

            var listado = Texto.UnaLinea(fila[columnas.Listados]);
            if (listado.Length > 0 && listado != "-")
            {
                listados.TryAdd(codigo, listado);
            }
        }

        var transitorios = datos.Transitorios.Select(t => t.Codigo).ToHashSet(StringComparer.Ordinal);
        var faltantes = transitorios.Where(t => !descripciones.ContainsKey(t)).ToList();
        if (faltantes.Count > 0)
        {
            throw new InvalidDataException($"Códigos transitorios curados que no existen en el Excel: {string.Join(", ", faltantes)}.");
        }

        return [.. descripciones
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new ErrorSunat(
                p.Key,
                p.Value,
                Tipo(p.Key),
                transitorios.Contains(p.Key),
                condiciones.TryGetValue(p.Key, out var lista) ? lista : [],
                listados.GetValueOrDefault(p.Key)))];
    }

    private static string Tipo(string codigo) => int.Parse(codigo, System.Globalization.CultureInfo.InvariantCulture) switch
    {
        < 2000 => "servicio",
        < 4000 => "rechazo",
        _ => "observacion",
    };

    private static (string Condicion, string Codigo, string Descripcion, string Listados) Columnas(HojaExcel general)
    {
        var encabezado = general.Filas.First(f => f.Celdas.Values.Any(v => Texto.Clave(v) == "CODIGO RETORNO"));
        string Buscar(Func<string, bool> criterio) =>
            encabezado.Celdas.First(c => criterio(Texto.Clave(c.Value))).Key;

        return (
            Buscar(c => c.StartsWith("VALIDACION", StringComparison.Ordinal)),
            Buscar(c => c == "CODIGO RETORNO"),
            Buscar(c => c.StartsWith("DESC", StringComparison.Ordinal)),
            Buscar(c => c == "LISTADOS"));
    }
}
