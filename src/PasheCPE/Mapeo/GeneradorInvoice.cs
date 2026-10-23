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
/// el XSD → valida las XSL de SUNAT y las reglas complementarias → escribe. Con cualquier error no escribe nada; las
/// observaciones de SUNAT van a las advertencias. La firma, el ZIP y el QR los agrega <c>ServicioCpe</c>.
/// </summary>
internal sealed class GeneradorInvoice(
    ValidadorModelo validador,
    CalculadoraTributaria calculadora,
    MapeadorInvoice mapeador,
    ValidadorEsquema esquema,
    ValidadorSunat sunat,
    AlmacenCpe almacen,
    Tiempo.RelojCpe reloj)
{
    /// <summary>Referencia del comprobante: la fecha de emisión, o hoy en Lima si no se indica.</summary>
    public ReferenciaCpe Referencia(EmisorCpe emisor, ComprobanteCpe cpe)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(cpe);
        return ReferenciaCpe.Comprobante(emisor.Ruc, cpe.TipoDocumento, cpe.Serie, cpe.Numero, cpe.FechaEmision ?? reloj.HoyLima());
    }

    /// <summary>Genera el XML y, si no hay errores, lo escribe en DATA.</summary>
    public async Task<XmlGenerado> GenerarAsync(EmisorCpe emisor, ComprobanteCpe cpe, CancellationToken ct)
    {
        var generado = Construir(emisor, cpe);
        if (!generado.Exito)
        {
            return generado;
        }

        var ruta = almacen.Ruta(CarpetaCpe.Data, generado.Referencia, NombreArchivoCpe.Xml(generado.Referencia));
        await AlmacenCpe.EscribirAsync(ruta, generado.Xml!, ct).ConfigureAwait(false);
        return generado with { Ruta = ruta };
    }

    /// <summary>Valida, calcula, mapea, serializa y valida el XML, sin escribir nada (validación de nivel 1).</summary>
    public XmlGenerado Construir(EmisorCpe emisor, ComprobanteCpe cpe)
    {
        var referencia = Referencia(emisor, cpe);

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

        var validacion = sunat.Validar(xml, referencia);
        IReadOnlyList<ObservacionCpe> advertencias = [.. calculo.Advertencias, .. validacion.Observaciones];
        if (validacion.Errores.Count > 0)
        {
            return new XmlGenerado(referencia, xml, null, validacion.Errores, advertencias, calculo);
        }

        return new XmlGenerado(referencia, xml, null, [], advertencias, calculo);
    }
}
