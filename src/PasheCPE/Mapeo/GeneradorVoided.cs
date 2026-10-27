using Microsoft.Extensions.Options;
using PasheCPE.Serializacion;
using PasheCPE.Validacion;

namespace PasheCPE.Mapeo;

/// <summary>
/// Comunicación de baja → XML sin firmar: valida el modelo y el historial de los documentos → mapea → serializa →
/// valida el XSD UBL 2.0 y la XSL de SUNAT. No escribe nada ni divide en lotes: <c>GeneracionResumenes</c> escribe,
/// firma y empaqueta una RA por llamada. Cada capa respeta su opción (<see cref="OpcionesValidacion"/>).
/// </summary>
internal sealed class GeneradorVoided(
    ValidadorBaja validador,
    MapeadorVoided mapeador,
    ValidadorEsquema esquema,
    ValidadorSunat sunat,
    IOptions<OpcionesPasheCpe> opciones)
{
    /// <summary>Validación del modelo y del historial de los documentos; vacía si <c>Validacion.Modelo = false</c>.</summary>
    public async Task<(IReadOnlyList<ErrorCpe> Errores, IReadOnlyList<ObservacionCpe> Advertencias)> ValidarAsync(EmisorCpe emisor, ComunicacionBajaCpe baja, DateOnly fechaGeneracion, CancellationToken ct)
    {
        if (!opciones.Value.Validacion.Modelo)
        {
            return ([], []);
        }

        var (errores, advertencias) = validador.Validar(emisor, baja, fechaGeneracion);
        if (errores.Count > 0)
        {
            return (errores, advertencias);
        }

        return (await validador.ValidarHistorialAsync(emisor, baja, ct).ConfigureAwait(false), advertencias);
    }

    /// <summary>XML de la baja validado con el XSD y la XSL; con errores, el XML se devuelve pero no se escribe.</summary>
    public XmlGenerado Construir(LoteBaja lote)
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
}
