using System.Globalization;
using System.Text.RegularExpressions;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Tiempo;

namespace PasheCPE.Validacion;

/// <summary>
/// Validación de la comunicación de baja (TRD §9.1, nivel 1), con los códigos del Excel de SUNAT entre paréntesis.
/// Acumula todos los errores y nunca lanza. <see cref="Validar"/> revisa el modelo y el plazo de 7 días desde la emisión
/// (2957, salvo las series numéricas, como dice el Excel); <see cref="ValidarHistorialAsync"/>, lo que consta en el
/// historial de cada documento generado por PasheCPE (los demás no se revisan): que SUNAT lo haya aceptado, que no esté
/// ya de baja o en otra comunicación y que su fecha de emisión sea la de la comunicación.
/// </summary>
internal sealed partial class ValidadorBaja(
    CatalogoErrores errores,
    TablaPlazos plazos,
    RegistroHistorial historial,
    RelojCpe reloj)
{
    private const int _longitudMinimaMotivo = 3;

    public (IReadOnlyList<ErrorCpe> Errores, IReadOnlyList<ObservacionCpe> Advertencias) Validar(EmisorCpe emisor, ComunicacionBajaCpe baja, DateOnly fechaGeneracion)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(baja);
        var lista = new List<ErrorCpe>();
        var advertencias = new List<ObservacionCpe>();
        var hoy = reloj.HoyLima();

        ErrorCpe Local(string codigo, string ruta, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);

        if (!DocumentosIdentidad.EsRucValido(emisor.Ruc))
        {
            lista.Add(Local("PCPE-1201", "Emisor.Ruc", emisor.Ruc));
        }

        if (string.IsNullOrWhiteSpace(emisor.RazonSocial))
        {
            lista.Add(Local("PCPE-1202", "Emisor.RazonSocial", "la razón social del emisor"));
        }

        if (baja.Documentos.Count == 0)
        {
            lista.Add(Local("PCPE-1501", "Documentos"));
            return (lista, advertencias);
        }

        if (baja.FechaReferencia > fechaGeneracion)
        {
            lista.Add(Local("PCPE-1502", "FechaReferencia", Texto(baja.FechaReferencia), Texto(fechaGeneracion)));
        }

        if (fechaGeneracion > hoy)
        {
            lista.Add(Local("PCPE-1503", "FechaGeneracion", Texto(fechaGeneracion), Texto(hoy)));
        }

        var limite = plazos.FechaLimite(ReferenciaCpe.TipoComunicacionBaja, null, baja.FechaReferencia);
        for (var i = 0; i < baja.Documentos.Count; i++)
        {
            var documento = baja.Documentos[i];
            var ruta = string.Create(CultureInfo.InvariantCulture, $"Documentos[{i}]");
            var nombre = Nombre(documento.TipoDocumento, documento.Serie, documento.Numero);
            if (EsDeBoleta(documento))
            {
                lista.Add(Local("PCPE-1505", ruta + ".TipoDocumento", nombre));
                continue;
            }

            if (documento.TipoDocumento is not (TipoDocumentoSunat.Factura or TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito))
            {
                lista.Add(Local("PCPE-1504", ruta + ".TipoDocumento", documento.TipoDocumento, nombre));
                continue;
            }

            if (!SerieBaja().IsMatch(documento.Serie ?? string.Empty))
            {
                lista.Add(Local("PCPE-1506", ruta + ".Serie", documento.Serie, nombre));
            }

            if (documento.Numero is < 1 or > 99_999_999)
            {
                lista.Add(Local("PCPE-1205", ruta + ".Numero", documento.Numero));
            }

            if (string.IsNullOrWhiteSpace(documento.Motivo))
            {
                lista.Add(Local("PCPE-1507", ruta + ".Motivo", nombre));
            }
            else if (documento.Motivo.Trim().Length < _longitudMinimaMotivo)
            {
                advertencias.Add(errores.Advertencia("PCPE-1508", nombre));
            }

            if (limite is { } fin && hoy > fin && !EsSerieNumerica(documento.Serie))
            {
                lista.Add(Local("PCPE-1510", ruta, nombre, Texto(fin)));
            }
        }

        foreach (var repetido in baja.Documentos.GroupBy(d => Nombre(d.TipoDocumento, d.Serie, d.Numero), StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            lista.Add(Local("PCPE-1509", "Documentos", repetido.Key));
        }

        return (lista, advertencias);
    }

    /// <summary>
    /// Contra el historial de cada documento generado por PasheCPE: aceptado por SUNAT (2105, 2398), no dado de baja ni
    /// en otra comunicación que aún no concluye (2323), y emitido en la fecha de la comunicación (2375).
    /// </summary>
    public async Task<IReadOnlyList<ErrorCpe>> ValidarHistorialAsync(EmisorCpe emisor, ComunicacionBajaCpe baja, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(baja);
        var lista = new List<ErrorCpe>();
        for (var i = 0; i < baja.Documentos.Count; i++)
        {
            var documento = baja.Documentos[i];
            var referencia = ReferenciaCpe.Comprobante(emisor.Ruc, documento.TipoDocumento, documento.Serie, documento.Numero, baja.FechaReferencia);
            if (await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false) is not { } previo)
            {
                continue;
            }

            var ruta = string.Create(CultureInfo.InvariantCulture, $"Documentos[{i}]");
            ErrorCpe Local(string codigo, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);
            if (previo.EstadoActual == EstadoCpe.DeBaja)
            {
                lista.Add(Local("PCPE-1512", referencia.NombreArchivo));
            }
            else if (previo.Baja is { } pendiente)
            {
                lista.Add(Local("PCPE-1513", referencia.NombreArchivo, pendiente.NombreArchivo));
            }
            else if (previo.EstadoActual is not (EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones))
            {
                lista.Add(Local("PCPE-1511", referencia.NombreArchivo, previo.EstadoActual));
            }

            if (previo.Referencia.Fecha != baja.FechaReferencia)
            {
                lista.Add(Local("PCPE-1514", referencia.NombreArchivo, Texto(previo.Referencia.Fecha), Texto(baja.FechaReferencia)));
            }
        }

        return lista;
    }

    /// <summary>true si la serie es numérica (comprobante físico): la regla 2957 del plazo no la alcanza.</summary>
    public static bool EsSerieNumerica(string? serie) => serie is { Length: > 0 } && serie.All(char.IsAsciiDigit);

    /// <summary>Boletas y sus notas (serie B): se anulan con el estado 3 en el resumen diario.</summary>
    private static bool EsDeBoleta(DocumentoBajaCpe documento) =>
        documento.TipoDocumento == TipoDocumentoSunat.BoletaDeVenta
        || (documento.TipoDocumento is TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito && documento.Serie is ['B', ..]);

    private static string Nombre(string tipo, string serie, long numero) => string.Create(CultureInfo.InvariantCulture, $"{tipo}-{serie}-{numero}");

    private static string Texto(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(F[A-Z0-9]{3}|[0-9]{1,4})$")]
    private static partial Regex SerieBaja();
}
