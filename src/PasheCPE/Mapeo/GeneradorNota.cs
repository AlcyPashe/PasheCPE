using Microsoft.Extensions.Options;
using PasheCPE.Serializacion;
using PasheCPE.Tributos;
using PasheCPE.Validacion;

namespace PasheCPE.Mapeo;

/// <summary>
/// Nota de crédito o débito → XML sin firmar en <c>DATA\yyyy\MM</c>: valida el modelo, sigue con
/// <see cref="ConstructorXml"/> (calcula → mapea → serializa → XSD → XSL de SUNAT y reglas complementarias) y valida la
/// nota contra el documento que modifica si está en el historial (<see cref="ValidadorDocumentoModificado"/>). Con
/// cualquier error no escribe nada. La firma, el ZIP y el QR los agrega <c>ServicioCpe</c>.
/// </summary>
internal sealed class GeneradorNota(
    ValidadorModelo validador,
    ValidadorDocumentoModificado modificado,
    MapeadorCreditNote credito,
    MapeadorDebitNote debito,
    ConstructorXml constructor,
    Tiempo.RelojCpe reloj,
    IOptions<OpcionesPasheCpe> opciones)
{
    /// <summary>Referencia de la nota: la fecha de emisión, o hoy en Lima si no se indica.</summary>
    public ReferenciaCpe Referencia(EmisorCpe emisor, NotaCpe nota)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(nota);
        return ReferenciaCpe.Comprobante(emisor.Ruc, nota.TipoDocumento, nota.Serie, nota.Numero, nota.FechaEmision ?? reloj.HoyLima());
    }

    /// <summary>Genera el XML y, si no hay errores, lo escribe en DATA.</summary>
    public async Task<XmlGenerado> GenerarAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct) =>
        await constructor.EscribirAsync(await ConstruirAsync(emisor, nota, ct).ConfigureAwait(false), ct).ConfigureAwait(false);

    /// <summary>Valida, calcula, mapea, serializa y valida el XML, sin escribir nada (validación de nivel 1).</summary>
    public async Task<XmlGenerado> ConstruirAsync(EmisorCpe emisor, NotaCpe nota, CancellationToken ct)
    {
        var referencia = Referencia(emisor, nota);
        var validarModelo = opciones.Value.Validacion.Modelo;
        var errores = validarModelo ? validador.Validar(emisor, nota) : [];
        if (errores.Count > 0)
        {
            return new XmlGenerado(referencia, null, null, errores, [], null);
        }

        var generado = constructor.Construir(referencia, emisor, nota, Serializar);
        if (generado.Calculo is not { } calculo)
        {
            return generado;
        }

        generado = generado with { Advertencias = [.. generado.Advertencias, .. validador.Advertencias(calculo)] };
        if (generado.Exito && validarModelo)
        {
            errores = await modificado.ValidarAsync(emisor, nota, calculo, ct).ConfigureAwait(false);
            if (errores.Count > 0)
            {
                generado = generado with { Errores = errores };
            }
        }

        return generado;
    }

    private byte[] Serializar(CalculoComprobante calculo) => calculo.Comprobante.TipoDocumento == TipoDocumentoSunat.NotaDeCredito
        ? SerializadorUbl.Serializar(credito.Mapear(calculo))
        : SerializadorUbl.Serializar(debito.Mapear(calculo));
}
