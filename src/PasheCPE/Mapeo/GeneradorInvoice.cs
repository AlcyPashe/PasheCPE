using Microsoft.Extensions.Options;
using PasheCPE.Serializacion;
using PasheCPE.Tributos;
using PasheCPE.Validacion;

namespace PasheCPE.Mapeo;

/// <summary>XML sin firmar de un comprobante, o los errores que impidieron generarlo.</summary>
internal sealed record XmlGenerado(
    ReferenciaCpe Referencia,
    byte[]? Xml,
    string? Ruta,
    IReadOnlyList<ErrorCpe> Errores,
    IReadOnlyList<ObservacionCpe> Advertencias,
    CalculoComprobante? Calculo)
{
    public bool Exito => Errores.Count == 0;
}

/// <summary>
/// Factura o boleta → XML sin firmar en <c>DATA\yyyy\MM</c>: valida el modelo y sigue con <see cref="ConstructorXml"/>
/// (calcula → mapea → serializa → XSD → XSL de SUNAT y reglas complementarias → escribe). Con cualquier error no
/// escribe nada. La firma, el ZIP y el QR los agrega <c>ServicioCpe</c>.
/// </summary>
internal sealed class GeneradorInvoice(
    ValidadorModelo validador,
    MapeadorInvoice mapeador,
    ConstructorXml constructor,
    Tiempo.RelojCpe reloj,
    IOptions<OpcionesPasheCpe> opciones)
{
    /// <summary>Referencia del comprobante: la fecha de emisión, o hoy en Lima si no se indica.</summary>
    public ReferenciaCpe Referencia(EmisorCpe emisor, ComprobanteCpe cpe)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(cpe);
        return ReferenciaCpe.Comprobante(emisor.Ruc, cpe.TipoDocumento, cpe.Serie, cpe.Numero, cpe.FechaEmision ?? reloj.HoyLima());
    }

    /// <summary>Genera el XML y, si no hay errores, lo escribe en DATA.</summary>
    public Task<XmlGenerado> GenerarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct) =>
        constructor.EscribirAsync(Construir(emisor, cpe), ct);

    /// <summary>Valida, calcula, mapea, serializa y valida el XML, sin escribir nada (validación de nivel 1).</summary>
    public XmlGenerado Construir(EmisorCpe emisor, ComprobanteCpe cpe)
    {
        var referencia = Referencia(emisor, cpe);
        var errores = opciones.Value.Validacion.Modelo ? validador.Validar(emisor, cpe) : [];
        return errores.Count > 0
            ? new XmlGenerado(referencia, null, null, errores, [], null)
            : constructor.Construir(referencia, emisor, cpe, calculo => SerializadorUbl.Serializar(mapeador.Mapear(calculo)));
    }
}
