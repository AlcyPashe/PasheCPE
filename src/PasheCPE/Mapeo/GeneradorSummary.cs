using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Serializacion;
using PasheCPE.Validacion;

namespace PasheCPE.Mapeo;

/// <summary>
/// Resumen diario → XML sin firmar de cada lote: valida el modelo → resuelve la tasa del IGV → divide en lotes de
/// <see cref="LineasPorLote"/> → mapea → serializa → valida el XSD UBL 2.0, la XSL de SUNAT y las reglas
/// complementarias. No escribe nada: <c>ServicioCpe</c> escribe, firma y empaqueta cada lote válido. Cada capa respeta su
/// opción (<see cref="OpcionesValidacion"/>).
/// </summary>
internal sealed class GeneradorSummary(
    ValidadorResumen validador,
    MapeadorSummary mapeador,
    ValidadorEsquema esquema,
    ValidadorSunat sunat,
    TablaTasas tasas,
    IOptions<OpcionesPasheCpe> opciones)
{
    /// <summary>Máximo de líneas por resumen (SUNAT).</summary>
    public const int LineasPorLote = 500;

    /// <summary>Validación del modelo y del historial de los documentos; vacía si <c>Validacion.Modelo = false</c>.</summary>
    public async Task<(IReadOnlyList<ErrorCpe> Errores, IReadOnlyList<ObservacionCpe> Advertencias)> ValidarAsync(EmisorCpe emisor, ResumenDiarioCpe resumen, DateOnly fechaGeneracion, CancellationToken ct)
    {
        if (!opciones.Value.Validacion.Modelo)
        {
            return ([], []);
        }

        var (errores, advertencias) = validador.Validar(emisor, resumen, fechaGeneracion);
        if (errores.Count > 0)
        {
            return (errores, advertencias);
        }

        return (await validador.ValidarHistorialAsync(emisor, resumen, ct).ConfigureAwait(false), advertencias);
    }

    /// <summary>Lotes de hasta 500 líneas con correlativos consecutivos desde <paramref name="primerCorrelativo"/>.</summary>
    public IReadOnlyList<LoteResumen> Lotes(EmisorCpe emisor, ResumenDiarioCpe resumen, DateOnly fechaGeneracion, int primerCorrelativo)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(resumen);
        var tasaPorDefecto = TasaIgv(emisor, resumen.FechaReferencia);
        var lineas = resumen.Lineas.Select(l => l.TasaIgv is null ? l with { TasaIgv = tasaPorDefecto } : l).ToList();
        return [.. lineas.Chunk(LineasPorLote).Select((lote, i) => new LoteResumen(
            emisor,
            ReferenciaCpe.Resumen(emisor.Ruc, fechaGeneracion, primerCorrelativo + i),
            resumen.FechaReferencia,
            lote))];
    }

    /// <summary>XML del lote validado con el XSD, la XSL y las reglas; con errores, el XML se devuelve pero no se escribe.</summary>
    public XmlGenerado Construir(LoteResumen lote)
    {
        ArgumentNullException.ThrowIfNull(lote);
        var xml = SerializadorUbl.Serializar(mapeador.Mapear(lote));
        IReadOnlyList<ErrorCpe> errores = opciones.Value.Validacion.Esquema ? esquema.Validar(xml) : [];
        if (errores.Count > 0)
        {
            return new XmlGenerado(lote.Referencia, xml, null, errores, [], null);
        }

        var resultado = sunat.Validar(xml, lote.Referencia);
        return new XmlGenerado(lote.Referencia, xml, null, resultado.Errores, resultado.Observaciones, null);
    }

    /// <summary>Tasa del IGV a la fecha de emisión: la tasa especial MYPE si el emisor la aplica y está vigente; si no, la general.</summary>
    private decimal TasaIgv(EmisorCpe emisor, DateOnly fecha) =>
        (emisor.TasaEspecialRestaurantesHoteles ? tasas.MypeRestaurantesHoteles(fecha) : null) ?? tasas.Igv(fecha).Tasa;
}
