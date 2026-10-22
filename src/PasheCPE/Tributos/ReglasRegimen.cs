using System.Globalization;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;

namespace PasheCPE.Tributos;

/// <summary>
/// Reglas por régimen del emisor (PRD RF-04): el NRUS solo emite boletas, sus notas, resúmenes y guías; la
/// exoneración de la Amazonía y la tasa especial MYPE cambian la afectación y la tasa por defecto. RER, RMT y
/// General no alteran el XML.
/// </summary>
internal sealed class ReglasRegimen(TablaTasas tasas, CatalogoErrores errores, IOptions<OpcionesPasheCpe> opciones)
{
    /// <summary>Error si el régimen del emisor no le permite emitir el tipo de documento; null si puede.</summary>
    public ErrorCpe? ValidarTipoDocumento(EmisorCpe emisor, string tipoDocumento, string? tipoDocumentoModificado = null)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        if (emisor.Regimen != RegimenTributario.NRUS)
        {
            return null;
        }

        var permitido = tipoDocumento switch
        {
            TipoDocumentoSunat.BoletaDeVenta
                or ReferenciaCpe.TipoResumenDiario
                or TipoDocumentoSunat.GuiaDeRemisionRemitente
                or TipoDocumentoSunat.GuiaDeRemisionTransportista => true,
            TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito => tipoDocumentoModificado == TipoDocumentoSunat.BoletaDeVenta,
            _ => false,
        };
        return permitido ? null : errores.Local("PCPE-1101", OrigenError.Modelo, "TipoDocumento", TipoDocumentoSunat.NumeroCatalogo, tipoDocumento);
    }

    /// <summary>Exoneración de la Amazonía del emisor, o la opción global si no la indica.</summary>
    public bool EsExoneradoAmazonia(EmisorCpe emisor)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        return emisor.ExoneradoAmazonia ?? opciones.Value.ExoneradoAmazoniaPorDefecto;
    }

    /// <summary>Afectación por defecto de las líneas: exonerado (20) en la Amazonía; si no, gravado (10).</summary>
    public string AfectacionPorDefecto(EmisorCpe emisor) =>
        EsExoneradoAmazonia(emisor) ? AfectacionIgv.ExoneradoOneroso : AfectacionIgv.GravadoOneroso;

    /// <summary>
    /// Tasa para la afectación gravada (10): la especial MYPE vigente si el emisor la tiene; si no hay tasa MYPE
    /// vigente a la fecha, la general, con una advertencia.
    /// </summary>
    public TasaAplicable TasaGravada(EmisorCpe emisor, DateOnly fechaEmision)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        var general = tasas.Igv(fechaEmision).Tasa;
        if (!emisor.TasaEspecialRestaurantesHoteles)
        {
            return new TasaAplicable(general, null);
        }

        return tasas.MypeRestaurantesHoteles(fechaEmision) is { } mype
            ? new TasaAplicable(mype, null)
            : new TasaAplicable(general, errores.Advertencia(
                "PCPE-1102",
                fechaEmision.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                general.ToString("0.00", CultureInfo.InvariantCulture)));
    }

    /// <summary>Leyenda de la Amazonía (catálogo N.° 52) según la naturaleza del ítem.</summary>
    public static string LeyendaAmazonia(TipoItem tipoItem) => tipoItem switch
    {
        TipoItem.Servicio => CodigoLeyenda.ServiciosAmazonia,
        TipoItem.Construccion => CodigoLeyenda.ConstruccionAmazonia,
        _ => CodigoLeyenda.BienesAmazonia,
    };
}

/// <summary>Tasa del IGV que se aplica y, si corresponde, la advertencia de por qué.</summary>
internal sealed record TasaAplicable(decimal Tasa, ObservacionCpe? Advertencia);
