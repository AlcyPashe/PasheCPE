using PasheCPE.Almacenamiento;
using PasheCPE.Empaquetado;
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
/// Factura o boleta → XML sin firmar en <c>DATA\yyyy\MM</c>: valida el modelo → calcula → mapea → serializa → valida
/// el XSD → escribe. Con cualquier error no escribe nada. La firma, el ZIP y el QR se agregan en la Fase 4.
/// </summary>
internal sealed class GeneradorInvoice(
    ValidadorModelo validador,
    CalculadoraTributaria calculadora,
    MapeadorInvoice mapeador,
    ValidadorEsquema esquema,
    AlmacenCpe almacen,
    Tiempo.RelojCpe reloj)
{
    public async Task<XmlGenerado> GenerarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(cpe);
        var referencia = ReferenciaCpe.Comprobante(emisor.Ruc, cpe.TipoDocumento, cpe.Serie, cpe.Numero, cpe.FechaEmision ?? reloj.HoyLima());

        var errores = validador.Validar(emisor, cpe);
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, null, null, errores, [], null);
        }

        var calculo = calculadora.Calcular(emisor, cpe);
        errores = [.. calculo.Errores, .. validador.ValidarTotales(calculo)];
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, null, null, errores, calculo.Advertencias, calculo);
        }

        var xml = SerializadorUbl.Serializar(mapeador.Mapear(calculo));
        errores = esquema.Validar(xml);
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, xml, null, errores, calculo.Advertencias, calculo);
        }

        var ruta = almacen.Ruta(CarpetaCpe.Data, referencia, NombreArchivoCpe.Xml(referencia));
        await AlmacenCpe.EscribirAsync(ruta, xml, ct).ConfigureAwait(false);
        return new XmlGenerado(referencia, xml, ruta, [], calculo.Advertencias, calculo);
    }
}
