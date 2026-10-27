using Microsoft.Extensions.Options;
using PasheCPE.Serializacion;
using PasheCPE.Tiempo;
using PasheCPE.Validacion;

namespace PasheCPE.Mapeo;

/// <summary>
/// Guía de remisión remitente → XML sin firmar en <c>DATA\yyyy\MM</c>: valida el modelo → mapea → serializa → valida el
/// XSD UBL 2.1 → valida la XSL GRE 2.0 de SUNAT. Con cualquier error no escribe nada; las observaciones de la XSL van a
/// las advertencias. La firma y el ZIP los agrega <c>ServicioGre</c>. Cada capa respeta su opción
/// (<see cref="OpcionesValidacion"/>).
/// </summary>
internal sealed class GeneradorGuia(
    ValidadorGuia validador,
    MapeadorDespatchAdvice mapeador,
    ValidadorEsquema esquema,
    ValidadorSunat sunat,
    ConstructorXml constructor,
    RelojCpe reloj,
    IOptions<OpcionesPasheCpe> opciones)
{
    /// <summary>Referencia de la guía: la fecha de emisión, o hoy en Lima si no se indica.</summary>
    public ReferenciaCpe Referencia(EmisorCpe emisor, GuiaRemisionCpe guia)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(guia);
        return ReferenciaCpe.Guia(emisor.Ruc, TipoDocumentoSunat.GuiaDeRemisionRemitente, guia.Serie, guia.Numero, guia.FechaEmision ?? reloj.HoyLima());
    }

    /// <summary>Genera el XML y, si no hay errores, lo escribe en DATA.</summary>
    public async Task<XmlGenerado> GenerarAsync(EmisorCpe emisor, GuiaRemisionCpe guia, CancellationToken ct) =>
        await constructor.EscribirAsync(Construir(emisor, guia), ct).ConfigureAwait(false);

    /// <summary>Valida, mapea, serializa y valida el XML, sin escribir nada (validación de nivel 1).</summary>
    public XmlGenerado Construir(EmisorCpe emisor, GuiaRemisionCpe guia)
    {
        var referencia = Referencia(emisor, guia);
        var errores = opciones.Value.Validacion.Modelo ? validador.Validar(emisor, guia, referencia.Fecha) : [];
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, null, null, errores, [], null);
        }

        var xml = SerializadorUbl.Serializar(mapeador.Mapear(emisor, guia, referencia, guia.HoraEmision ?? TimeOnly.FromDateTime(reloj.AhoraLima().DateTime)));
        errores = opciones.Value.Validacion.Esquema ? esquema.Validar(xml) : [];
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, xml, null, errores, [], null);
        }

        var resultado = sunat.Validar(xml, referencia);
        return new XmlGenerado(referencia, xml, null, resultado.Errores, resultado.Observaciones, null);
    }
}
