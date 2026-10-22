namespace PasheCPE.GeneradorRecursos;

/// <summary>Catálogo del Anexo N.° 8 tal como se publica en la hoja «Catálogos».</summary>
internal sealed record Catalogo(
    string Numero,
    string Nombre,
    IReadOnlyList<string> Columnas,
    string? Formato,
    IReadOnlyList<string> Enlace,
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<ItemCatalogo> Items);

/// <summary>Código de un catálogo con sus columnas adicionales y su vigencia.</summary>
internal sealed record ItemCatalogo(
    string Codigo,
    string Descripcion,
    IReadOnlyList<KeyValuePair<string, string>> Extras,
    DateOnly? Desde,
    DateOnly? Hasta);

/// <summary>
/// Lee la hoja «Catálogos»: bloques con una fila «No.», una fila «Catálogo», la fila de encabezados y los datos
/// hasta el siguiente «No.». Los catálogos publicados solo como enlace se validan por formato (datos curados).
/// </summary>
internal static class LectorCatalogos
{
    // Columnas (código, descripción) cuando no son las dos primeras del bloque.
    private static readonly Dictionary<string, (string Codigo, string Descripcion)> _columnasEspeciales = new(StringComparer.Ordinal)
    {
        ["24"] = ("A", "C"),
        ["25.1"] = ("B", "C"),
    };

    public static IReadOnlyList<Catalogo> Leer(HojaExcel hoja, DatosCurados datos)
    {
        var formatos = datos.Formatos.ToDictionary(f => f.Catalogo, StringComparer.Ordinal);
        var catalogos = new List<Catalogo>();
        var filas = hoja.Filas;

        for (var i = 0; i < filas.Count; i++)
        {
            if (!EsInicioBloque(filas[i]))
            {
                continue;
            }

            var numero = NumeroCatalogo(filas[i]["B"]);
            var nombre = i + 1 < filas.Count && Texto.Clave(filas[i + 1]["A"]) == "CATALOGO" ? Texto.UnaLinea(filas[i + 1]["B"]) : string.Empty;
            var encabezado = filas[i + 2];
            var columnas = encabezado.Celdas.OrderBy(c => c.Key.Length).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
            var (columnaCodigo, columnaDescripcion) = _columnasEspeciales.TryGetValue(numero, out var especial)
                ? especial
                : (columnas[0].Key, columnas.Count > 1 ? columnas[1].Key : columnas[0].Key);

            var datosBloque = new List<FilaExcel>();
            for (var j = i + 3; j < filas.Count && !EsInicioBloque(filas[j]); j++)
            {
                datosBloque.Add(filas[j]);
            }

            var formato = formatos.GetValueOrDefault(numero);
            var items = new List<ItemCatalogo>();
            var enlace = new List<string>();
            if (formato is not null)
            {
                enlace.AddRange(datosBloque.SelectMany(f => f.Celdas.Values).Select(Texto.UnaLinea).Where(t => t.Length > 0));
            }
            else
            {
                foreach (var fila in datosBloque)
                {
                    var codigo = Texto.UnaLinea(fila[columnaCodigo]);
                    if (codigo.Length == 0)
                    {
                        continue;
                    }

                    var extras = columnas
                        .Where(c => c.Key != columnaCodigo && c.Key != columnaDescripcion && fila[c.Key] is not null)
                        .Select(c => new KeyValuePair<string, string>(Texto.UnaLinea(c.Value), Texto.UnaLinea(fila[c.Key])))
                        .ToList();
                    items.Add(new ItemCatalogo(codigo, Texto.UnaLinea(fila[columnaDescripcion]), extras, null, null));
                }
            }

            catalogos.Add(new Catalogo(numero, nombre, [.. columnas.Select(c => Texto.UnaLinea(c.Value))], formato?.Formato, enlace, null, null, items));
        }

        return AplicarVigencias(catalogos, datos.Vigencias);
    }

    private static bool EsInicioBloque(FilaExcel fila) => Texto.Clave(fila["A"]) == "NO.";

    private static string NumeroCatalogo(string? valor)
    {
        var numero = Texto.UnaLinea(valor);
        return numero.Length == 1 ? "0" + numero : numero;
    }

    private static List<Catalogo> AplicarVigencias(List<Catalogo> catalogos, IReadOnlyList<VigenciaCurada> vigencias)
    {
        var resultado = catalogos.ToDictionary(c => c.Numero, StringComparer.Ordinal);
        foreach (var vigencia in vigencias)
        {
            if (!resultado.TryGetValue(vigencia.Catalogo, out var catalogo))
            {
                throw new InvalidDataException($"Vigencia curada para un catálogo que no existe en el Excel: {vigencia.Catalogo}.");
            }

            if (vigencia.Codigo is null)
            {
                resultado[vigencia.Catalogo] = catalogo with { Desde = vigencia.Desde, Hasta = vigencia.Hasta };
                continue;
            }

            if (!catalogo.Items.Any(i => i.Codigo == vigencia.Codigo))
            {
                throw new InvalidDataException($"Vigencia curada para un código que no existe: catálogo {vigencia.Catalogo}, código {vigencia.Codigo}.");
            }

            resultado[vigencia.Catalogo] = catalogo with
            {
                Items = [.. catalogo.Items.Select(i => i.Codigo == vigencia.Codigo ? i with { Desde = vigencia.Desde, Hasta = vigencia.Hasta } : i)],
            };
        }

        return [.. catalogos.Select(c => resultado[c.Numero])];
    }
}
