using System.Globalization;
using System.Text.RegularExpressions;
using PasheCPE.Almacenamiento;
using PasheCPE.Catalogos;
using PasheCPE.Tiempo;

namespace PasheCPE.Validacion;

/// <summary>
/// Validación del modelo del resumen diario (TRD §9.1, nivel 1), con los códigos del Excel de SUNAT entre paréntesis.
/// Acumula todos los errores y nunca lanza. <see cref="Validar"/> revisa el modelo; <see cref="ValidarHistorialAsync"/>,
/// lo que ya consta en el historial de cada documento (no adicionar dos veces, no modificar lo que SUNAT no tiene).
/// Fuera de plazo el resumen se envía con una advertencia (SUNAT lo acepta con la observación 4443), salvo las
/// anulaciones, que SUNAT rechaza (2957).
/// </summary>
internal sealed partial class ValidadorResumen(
    CatalogosSunat catalogos,
    CatalogoErrores errores,
    TablaPlazos plazos,
    RegistroHistorial historial,
    RelojCpe reloj,
    ParametrosSunat parametros)
{
    public (IReadOnlyList<ErrorCpe> Errores, IReadOnlyList<ObservacionCpe> Advertencias) Validar(EmisorCpe emisor, ResumenDiarioCpe resumen, DateOnly fechaGeneracion)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(resumen);
        var lista = new List<ErrorCpe>();
        var advertencias = new List<ObservacionCpe>();
        var hoy = reloj.HoyLima();

        void Agregar(ErrorCpe? error)
        {
            if (error is not null)
            {
                lista.Add(error);
            }
        }

        ErrorCpe Local(string codigo, string ruta, params object?[] argumentos) => errores.Local(codigo, OrigenError.Modelo, ruta, null, argumentos);

        if (!DocumentosIdentidad.EsRucValido(emisor.Ruc))
        {
            lista.Add(Local("PCPE-1201", "Emisor.Ruc", emisor.Ruc));
        }

        if (string.IsNullOrWhiteSpace(emisor.RazonSocial))
        {
            lista.Add(Local("PCPE-1202", "Emisor.RazonSocial", "la razón social del emisor"));
        }

        if (resumen.Lineas.Count == 0)
        {
            lista.Add(Local("PCPE-1301", "Lineas"));
            return (lista, advertencias);
        }

        if (resumen.FechaReferencia > fechaGeneracion)
        {
            lista.Add(Local("PCPE-1302", "FechaReferencia", Texto(resumen.FechaReferencia), Texto(fechaGeneracion)));
        }

        if (fechaGeneracion > hoy)
        {
            lista.Add(Local("PCPE-1303", "FechaGeneracion", Texto(fechaGeneracion), Texto(hoy)));
        }

        var limite = plazos.FechaLimite(ReferenciaCpe.TipoResumenDiario, null, resumen.FechaReferencia);
        var vencido = limite is { } fin && hoy > fin;
        for (var i = 0; i < resumen.Lineas.Count; i++)
        {
            var linea = resumen.Lineas[i];
            var ruta = string.Create(CultureInfo.InvariantCulture, $"Lineas[{i}]");
            var numeroLinea = i + 1;
            var documento = Nombre(linea.TipoDocumento, linea.Serie, linea.Numero);
            var esNota = linea.TipoDocumento is TipoDocumentoSunat.NotaDeCredito or TipoDocumentoSunat.NotaDeDebito;
            if (!esNota && linea.TipoDocumento != TipoDocumentoSunat.BoletaDeVenta)
            {
                lista.Add(Local("PCPE-1304", ruta + ".TipoDocumento", linea.TipoDocumento, numeroLinea));
                continue;
            }

            if (!SerieBoleta().IsMatch(linea.Serie ?? string.Empty))
            {
                lista.Add(Local("PCPE-1305", ruta + ".Serie", linea.Serie, numeroLinea));
            }

            if (linea.Numero is < 1 or > 99_999_999)
            {
                lista.Add(Local("PCPE-1205", ruta + ".Numero", linea.Numero));
            }

            Agregar(catalogos.Validar(EstadoItemResumen.NumeroCatalogo, linea.Estado, fechaGeneracion, ruta + ".Estado"));
            Agregar(catalogos.Validar("02", linea.Moneda, resumen.FechaReferencia, ruta + ".Moneda"));

            var cliente = linea.Cliente;
            if (cliente.TipoDocumento != "-")
            {
                Agregar(catalogos.Validar(TipoDocumentoIdentidad.NumeroCatalogo, cliente.TipoDocumento, resumen.FechaReferencia, ruta + ".Cliente.TipoDocumento"));
            }

            if (!DocumentosIdentidad.EsAnonimo(cliente) && !DocumentosIdentidad.TieneFormato(cliente.TipoDocumento, cliente.NumeroDocumento))
            {
                lista.Add(Local("PCPE-1212", ruta + ".Cliente.NumeroDocumento", cliente.NumeroDocumento, cliente.TipoDocumento));
            }

            if (linea.Moneda == "PEN" && linea.ImporteTotal > parametros.BoletaSinIdentificacionHasta && DocumentosIdentidad.EsAnonimo(cliente))
            {
                lista.Add(Local("PCPE-1211", ruta + ".Cliente", parametros.BoletaSinIdentificacionHasta.ToString("0.00", CultureInfo.InvariantCulture)));
            }

            if (esNota)
            {
                if (linea.DocumentoModificado is { } modificado)
                {
                    if (modificado.TipoDocumento is not (TipoDocumentoSunat.BoletaDeVenta or "12" or "16" or "55"))
                    {
                        lista.Add(Local("PCPE-1307", ruta + ".DocumentoModificado.TipoDocumento", documento, modificado.TipoDocumento));
                    }
                }
                else if (linea.Estado != EstadoItemResumen.Anulado)
                {
                    lista.Add(Local("PCPE-1306", ruta + ".DocumentoModificado", documento, numeroLinea));
                }
            }
            else if (linea.DocumentoModificado is not null)
            {
                lista.Add(Local("PCPE-1308", ruta + ".DocumentoModificado", documento, numeroLinea));
            }

            decimal[] importes =
            [
                linea.TotalGravado, linea.TotalExonerado, linea.TotalInafecto, linea.TotalExportacion, linea.TotalGratuitoGravado,
                linea.TotalGratuitoExonerado, linea.TotalGratuitoInafecto, linea.TotalGratuitoExportacion, linea.OtrosCargos, linea.Igv,
                linea.IgvGratuitas, linea.Isc, linea.Icbper, linea.ImporteTotal,
            ];
            if (importes.Any(m => m < 0))
            {
                lista.Add(Local("PCPE-1309", ruta, numeroLinea));
            }

            if (linea.TasaIgv is <= 0)
            {
                lista.Add(Local("PCPE-1320", ruta + ".TasaIgv", numeroLinea));
            }

            if (vencido && linea.Estado == EstadoItemResumen.Anulado)
            {
                lista.Add(Local("PCPE-1313", ruta + ".Estado", documento, Texto(limite!.Value)));
            }
        }

        Repetidos(resumen, lista, Local);
        if (vencido && resumen.Lineas.Any(l => l.Estado != EstadoItemResumen.Anulado))
        {
            advertencias.Add(errores.Advertencia("PCPE-1318", Texto(resumen.FechaReferencia), Texto(limite!.Value)));
        }

        return (lista, advertencias);
    }

    /// <summary>
    /// Contra el historial de cada documento generado por PasheCPE (los que no tienen historial no se revisan): no se
    /// adiciona lo que SUNAT ya tiene o está procesando (2282) y solo se modifica o anula lo que ya aceptó (2663).
    /// </summary>
    public async Task<IReadOnlyList<ErrorCpe>> ValidarHistorialAsync(EmisorCpe emisor, ResumenDiarioCpe resumen, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emisor);
        ArgumentNullException.ThrowIfNull(resumen);
        var lista = new List<ErrorCpe>();
        for (var i = 0; i < resumen.Lineas.Count; i++)
        {
            var linea = resumen.Lineas[i];
            var referencia = ReferenciaCpe.Comprobante(emisor.Ruc, linea.TipoDocumento, linea.Serie, linea.Numero, resumen.FechaReferencia);
            if (await historial.ObtenerAsync(referencia, ct).ConfigureAwait(false) is not { } previo)
            {
                continue;
            }

            var ruta = string.Create(CultureInfo.InvariantCulture, $"Lineas[{i}]");
            var informado = previo.EstadoActual is EstadoCpe.EnProceso or EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones or EstadoCpe.Rechazado or EstadoCpe.RespuestaIncierta;
            if (linea.Estado == EstadoItemResumen.Adicionar && informado)
            {
                lista.Add(errores.Local("PCPE-1314", OrigenError.Modelo, ruta, null, referencia.NombreArchivo, previo.EstadoActual));
            }
            else if (linea.Estado != EstadoItemResumen.Adicionar && previo.EstadoActual is not (EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones))
            {
                lista.Add(errores.Local("PCPE-1315", OrigenError.Modelo, ruta, null, referencia.NombreArchivo, previo.EstadoActual));
            }
        }

        return lista;
    }

    /// <summary>3094 (mismo documento y estado repetido), 3095 (adiciona y modifica) y 3096 (modifica y anula).</summary>
    private static void Repetidos(ResumenDiarioCpe resumen, List<ErrorCpe> lista, Func<string, string, object?[], ErrorCpe> local)
    {
        foreach (var grupo in resumen.Lineas.GroupBy(l => Nombre(l.TipoDocumento, l.Serie, l.Numero), StringComparer.Ordinal))
        {
            var estados = grupo.Select(l => l.Estado).ToList();
            foreach (var repetido in estados.GroupBy(e => e, StringComparer.Ordinal).Where(g => g.Count() > 1))
            {
                lista.Add(local("PCPE-1310", "Lineas", [grupo.Key, repetido.Key]));
            }

            if (estados.Contains(EstadoItemResumen.Adicionar) && estados.Contains(EstadoItemResumen.Modificar))
            {
                lista.Add(local("PCPE-1311", "Lineas", [grupo.Key]));
            }

            if (estados.Contains(EstadoItemResumen.Modificar) && estados.Contains(EstadoItemResumen.Anulado))
            {
                lista.Add(local("PCPE-1312", "Lineas", [grupo.Key]));
            }
        }
    }

    private static string Nombre(string tipo, string serie, long numero) => string.Create(CultureInfo.InvariantCulture, $"{tipo}-{serie}-{numero}");

    private static string Texto(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(B[A-Z0-9]{3}|[0-9]{1,4})$")]
    private static partial Regex SerieBoleta();
}
