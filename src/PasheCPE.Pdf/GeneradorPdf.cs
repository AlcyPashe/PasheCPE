using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Empaquetado;
using PasheCPE.Pdf.Diagnostico;
using PasheCPE.Pdf.Disenos;
using PasheCPE.Pdf.Representacion;
using PasheCPE.Servicios;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PasheCPE.Pdf;

/// <summary>
/// Genera el PDF A4 (TRD §22): <see cref="LectorRepresentacion"/> lee el XML firmado y el CDR, se elige
/// <see cref="DisenoComprobante"/> (01, 03, 07, 08) o <see cref="DisenoGuia"/> (09) y se guarda de forma atómica en
/// <c>REPO\yyyy\MM\{nombre}.pdf</c>. También es el gancho de la generación automática dentro de <c>EmitirAsync</c>,
/// con la marca del RUC registrada en <see cref="OpcionesPasheCpePdf"/>. Nunca lanza por errores del documento.
/// </summary>
internal sealed partial class GeneradorPdf(
    LectorRepresentacion lector,
    AlmacenCpe almacen,
    CatalogoErrores errores,
    IOptions<OpcionesPasheCpePdf> opcionesPdf,
    ILogger<GeneradorPdf> registro) : IGeneradorPdf, IGeneracionPdfAutomatica
{
    private const string _colorPorDefecto = "#1F3A5F";

    public async Task<ResultadoPdf> GenerarAsync(ReferenciaCpe referencia, MarcaEmisor marca, DatosAdicionalesPdf? extra = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        ArgumentNullException.ThrowIfNull(marca);
        var lectura = await lector.LeerAsync(referencia, ct).ConfigureAwait(false);
        if (lectura.Error is { } error)
        {
            RegistroEventosPdf.PdfNoGenerado(registro, referencia.NombreArchivo, error.Codigo, error.Descripcion);
            return new ResultadoPdf { Referencia = referencia, Errores = [error] };
        }

        var advertencias = new List<ObservacionCpe>();
        var estilo = new EstiloPdf(marca, await LogoAsync(marca, advertencias, ct).ConfigureAwait(false), Color(marca.ColorPrincipal), extra);
        IDocument documento = lectura.Comprobante is { } comprobante
            ? new DisenoComprobante(comprobante, estilo)
            : new DisenoGuia(lectura.Guia!, estilo);

        byte[] pdf;
        try
        {
            pdf = documento.GeneratePdf();
        }
#pragma warning disable CA1031 // Una falla de QuestPDF se devuelve como PCPE-5006: el PDF nunca interrumpe la venta.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            RegistroEventosPdf.PdfFallido(registro, referencia.NombreArchivo, ex);
            return new ResultadoPdf { Referencia = referencia, Errores = [errores.Local("PCPE-5006", OrigenError.Pdf, null, null, referencia.NombreArchivo, ex.Message)], Advertencias = advertencias };
        }

        var ruta = almacen.Ruta(CarpetaCpe.Repo, referencia, NombreArchivoCpe.Pdf(referencia));
        await AlmacenCpe.EscribirAsync(ruta, pdf, ct).ConfigureAwait(false);
        RegistroEventosPdf.PdfGenerado(registro, referencia.NombreArchivo, ruta);
        return new ResultadoPdf { Referencia = referencia, Ruta = ruta, Advertencias = advertencias };
    }

    async Task<(string? Ruta, string? Error)> IGeneracionPdfAutomatica.GenerarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct)
    {
        var resultado = await GenerarAsync(referencia, opcionesPdf.Value.MarcaDe(emisor.Ruc), null, ct).ConfigureAwait(false);
        return resultado.Exito ? (resultado.Ruta, null) : (null, resultado.Errores[0].Descripcion);
    }

    /// <summary>Logo de la marca, validado con QuestPDF; si no se puede usar, PDF sin logo y advertencia PCPE-5007.</summary>
    private async Task<byte[]?> LogoAsync(MarcaEmisor marca, List<ObservacionCpe> advertencias, CancellationToken ct)
    {
        byte[]? logo;
        try
        {
            logo = marca.Logo is { } bytes ? bytes.ToArray()
                : !string.IsNullOrWhiteSpace(marca.RutaLogo) ? await File.ReadAllBytesAsync(marca.RutaLogo, ct).ConfigureAwait(false)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            advertencias.Add(errores.Advertencia("PCPE-5007", ex.Message));
            return null;
        }

        if (logo is null)
        {
            return null;
        }

        try
        {
            using var imagen = Image.FromBinaryData(logo);
            return logo;
        }
#pragma warning disable CA1031 // QuestPDF no documenta la excepción de una imagen no válida.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            advertencias.Add(errores.Advertencia("PCPE-5007", ex.Message));
            return null;
        }
    }

    /// <summary>Color principal #RRGGBB; uno no válido se reemplaza por el color por defecto.</summary>
    private static Color Color(string? hex) =>
        QuestPDF.Infrastructure.Color.FromHex(hex is not null && ColorHex().IsMatch(hex) ? hex : _colorPorDefecto);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorHex();
}
