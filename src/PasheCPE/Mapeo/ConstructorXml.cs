using Microsoft.Extensions.Options;
using PasheCPE.Almacenamiento;
using PasheCPE.Empaquetado;
using PasheCPE.Tributos;
using PasheCPE.Validacion;

namespace PasheCPE.Mapeo;

/// <summary>
/// Pasos comunes a facturas, boletas y notas tras validar el modelo: calcula → valida los totales → mapea y serializa →
/// valida el XSD → valida las XSL de SUNAT y las reglas complementarias; y la escritura en <c>DATA\yyyy\MM</c>. Con
/// cualquier error se detiene; las observaciones de SUNAT van a las advertencias. Cada capa respeta su opción
/// (<see cref="OpcionesValidacion"/>); los errores del cálculo y de los totales bloquean siempre.
/// </summary>
internal sealed class ConstructorXml(
    ValidadorModelo validador,
    CalculadoraTributaria calculadora,
    ValidadorEsquema esquema,
    ValidadorSunat sunat,
    AlmacenCpe almacen,
    IOptions<OpcionesPasheCpe> opciones)
{
    public XmlGenerado Construir(ReferenciaCpe referencia, EmisorCpe emisor, DocumentoVentaCpe documento, Func<CalculoComprobante, byte[]> serializar)
    {
        ArgumentNullException.ThrowIfNull(serializar);
        var calculo = calculadora.Calcular(emisor, documento);
        IReadOnlyList<ErrorCpe> errores = [.. calculo.Errores, .. validador.ValidarTotales(calculo)];
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, null, null, errores, calculo.Advertencias, calculo);
        }

        var xml = serializar(calculo);
        errores = opciones.Value.Validacion.Esquema ? esquema.Validar(xml) : [];
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, xml, null, errores, calculo.Advertencias, calculo);
        }

        var resultadoSunat = sunat.Validar(xml, referencia);
        IReadOnlyList<ObservacionCpe> advertencias = [.. calculo.Advertencias, .. resultadoSunat.Observaciones];
        return new XmlGenerado(referencia, xml, null, resultadoSunat.Errores, advertencias, calculo);
    }

    /// <summary>Escribe el XML en DATA si se generó sin errores.</summary>
    public async Task<XmlGenerado> EscribirAsync(XmlGenerado generado, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(generado);
        if (!generado.Exito)
        {
            return generado;
        }

        var ruta = almacen.Ruta(CarpetaCpe.Data, generado.Referencia, NombreArchivoCpe.Xml(generado.Referencia));
        await AlmacenCpe.EscribirAsync(ruta, generado.Xml!, ct).ConfigureAwait(false);
        return generado with { Ruta = ruta };
    }
}
