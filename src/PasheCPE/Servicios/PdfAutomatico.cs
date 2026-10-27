using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;

namespace PasheCPE.Servicios;

/// <summary>
/// Gancho que implementa PasheCPE.Pdf (<c>AddPasheCpePdf</c>): el núcleo no conoce QuestPDF. Genera el PDF del
/// documento en <c>REPO\yyyy\MM</c> y devuelve su ruta o el motivo por el que no se generó. Nunca lanza.
/// </summary>
internal interface IGeneracionPdfAutomatica
{
    Task<(string? Ruta, string? Error)> GenerarAsync(EmisorCpe emisor, ReferenciaCpe referencia, CancellationToken ct);
}

/// <summary>
/// PDF dentro de <c>EmitirAsync</c> (TRD §22) si PasheCPE.Pdf está registrado y
/// <see cref="OpcionesPasheCpe.GenerarPdfAutomaticamente"/>: solo para documentos aceptados o boletas y notas B que
/// esperan el resumen diario (<c>PCPE-4002</c>); nunca para rechazos ni errores. Si el PDF falla, la emisión no cambia
/// y se agrega la advertencia <c>PCPE-5001</c> a la generación.
/// </summary>
internal sealed class PdfAutomatico(IEnumerable<IGeneracionPdfAutomatica> generadores, IOptions<OpcionesPasheCpe> opciones, CatalogoErrores errores)
{
    public const string CodigoEnResumenDiario = "PCPE-4002";

    public async Task<ResultadoEmision> AplicarAsync(EmisorCpe emisor, ResultadoEmision emision, CancellationToken ct)
    {
        if (!opciones.Value.GenerarPdfAutomaticamente || generadores.FirstOrDefault() is not { } generador || !Corresponde(emision.Envio))
        {
            return emision;
        }

        var generacion = emision.Generacion;
        var (ruta, error) = await generador.GenerarAsync(emisor, generacion.Referencia, ct).ConfigureAwait(false);
        if (ruta is not null)
        {
            return emision with
            {
                Generacion = generacion with { Rutas = generacion.Rutas with { Pdf = ruta } },
                Envio = emision.Envio with { Rutas = emision.Envio.Rutas with { Pdf = ruta } },
            };
        }

        var advertencia = errores.Advertencia("PCPE-5001", generacion.NombreArchivo, error);
        return emision with { Generacion = generacion with { Advertencias = [.. generacion.Advertencias, advertencia] } };
    }

    private static bool Corresponde(ResultadoEnvio envio) =>
        envio.Estado is EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones
        || (envio.Estado == EstadoCpe.ErrorValidacionLocal && envio.Codigo == CodigoEnResumenDiario);
}
