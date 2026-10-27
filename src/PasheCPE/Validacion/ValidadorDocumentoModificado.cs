using System.Globalization;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Mapeo;
using PasheCPE.Tributos;

namespace PasheCPE.Validacion;

/// <summary>
/// Valida la nota contra el documento que modifica cuando este lo generó PasheCPE: con la fecha de emisión del original
/// (<see cref="DocumentoModificadoCpe.FechaEmision"/>) se ubica su historial y su XML firmado. Reemplaza localmente las
/// reglas que SUNAT valida con su padrón:
/// <list type="bullet">
/// <item>el original no puede estar rechazado ni sin pasar la validación (2119, 2121, 2208, 2209) y, si es una factura,
/// debe estar aceptado;</item>
/// <item>misma moneda que la factura (3209);</item>
/// <item>el importe total de la nota de crédito no supera el del original (3286; en boletas con tolerancia de 1 y solo en
/// la misma moneda), salvo en el motivo 10 (otros conceptos).</item>
/// </list>
/// Si no hay fecha del original, o el original no está en el historial o en FIRMA, no valida nada. Nunca lanza.
/// </summary>
internal sealed class ValidadorDocumentoModificado(RegistroHistorial historial, LectorBoletaFirmada lector, CatalogoErrores errores)
{
    public async Task<IReadOnlyList<ErrorCpe>> ValidarAsync(EmisorCpe emisor, NotaCpe nota, CalculoComprobante calculo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(nota);
        ArgumentNullException.ThrowIfNull(calculo);
        if (nota.DocumentoModificado is not { FechaEmision: { } fecha } modificado)
        {
            return [];
        }

        var referencia = ReferenciaCpe.Comprobante(emisor.Ruc, modificado.TipoDocumento, modificado.Serie, modificado.Numero, fecha);
        if (await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false) is not { } previo)
        {
            return [];
        }

        var esFactura = modificado.TipoDocumento == TipoDocumentoSunat.Factura;
        var lista = new List<ErrorCpe>();
        ErrorCpe Local(string codigo, string ruta, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);

        if (previo.EstadoActual is EstadoCpe.Rechazado or EstadoCpe.ErrorValidacionLocal or EstadoCpe.DeBaja)
        {
            lista.Add(Local("PCPE-1430", "DocumentoModificado", referencia.NombreArchivo, previo.EstadoActual));
            return lista;
        }

        // Una boleta del resumen diario puede ir en el mismo resumen que su nota; una factura debe estar aceptada.
        if (esFactura && previo.EstadoActual is not (EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones))
        {
            lista.Add(Local("PCPE-1431", "DocumentoModificado", referencia.NombreArchivo, previo.EstadoActual));
        }

        var leido = await lector.LeerAsync(referencia, "1", ct).ConfigureAwait(false);
        if (leido.Linea is not { } original)
        {
            return lista;
        }

        var mismaMoneda = original.Moneda == nota.Moneda;
        if (esFactura && !mismaMoneda)
        {
            lista.Add(Local("PCPE-1432", "Moneda", nota.Moneda, original.Moneda));
        }

        var tolerancia = esFactura ? 0m : 1m;
        if (nota.TipoDocumento == TipoDocumentoSunat.NotaDeCredito
            && nota.Motivo != MotivoNotaCredito.OtrosConceptos
            && mismaMoneda
            && calculo.ImporteTotal > original.ImporteTotal + tolerancia)
        {
            lista.Add(Local("PCPE-1433", "Lineas", Monto(calculo.ImporteTotal), Monto(original.ImporteTotal)));
        }

        return lista;
    }

    private static string Monto(decimal monto) => monto.ToString("0.00", CultureInfo.InvariantCulture);
}
