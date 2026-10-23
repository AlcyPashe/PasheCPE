using System.Xml;
using Microsoft.Extensions.Options;
using PasheCPE.Catalogos;
using PasheCPE.Tiempo;
using PasheCPE.Validacion.Reglas;

namespace PasheCPE.Validacion;

/// <summary>
/// Validación SUNAT del XML sin firmar (TRD §9.3 y §9.4): XSL oficiales con emulación de DataPower, descarte de los
/// códigos de la XSL que reemplaza una regla C# (<c>ajustes-xsl.json</c>) y reglas complementarias del Excel vigente.
/// Cada capa respeta su opción (<see cref="OpcionesValidacion.XslSunat"/>, <see cref="OpcionesValidacion.Reglas2027"/>).
/// Los códigos se suprimen solo si las reglas complementarias están activas. Nunca lanza.
/// </summary>
internal sealed class ValidadorSunat(
    MotorXslSunat motor,
    IEnumerable<IReglaCpe> reglas,
    AjustesXsl ajustes,
    ReglasExcelSunat excel,
    CatalogoErrores errores,
    RelojCpe reloj,
    IOptions<OpcionesPasheCpe> opciones)
{
    private readonly IReadOnlyList<IReglaCpe> _reglas = [.. reglas];

    public ResultadoValidacionSunat Validar(byte[] xml, ReferenciaCpe referencia)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(referencia);
        var validacion = opciones.Value.Validacion;
        var encontrados = new List<ErrorCpe>();
        var observaciones = new List<ObservacionCpe>();

        if (validacion.XslSunat)
        {
            var resultado = motor.Validar(xml, referencia);
            if (resultado.Falla is { } falla)
            {
                encontrados.Add(falla);
            }

            foreach (var hallazgo in resultado.Hallazgos)
            {
                if (validacion.Reglas2027 && ajustes.EstaSuprimido(referencia.TipoDocumento, hallazgo.Codigo))
                {
                    continue;
                }

                Agregar(hallazgo.Codigo, hallazgo.EsError, hallazgo.Ruta, hallazgo.Detalle, OrigenError.XslSunat, null);
            }
        }

        if (validacion.Reglas2027)
        {
            EvaluarReglas(xml, referencia, Agregar);
        }

        return new ResultadoValidacionSunat(encontrados, observaciones);

        void Agregar(string codigo, bool esError, string? ruta, string detalle, OrigenError origen, string? mensajeExcel)
        {
            var mensaje = errores.Buscar(codigo)?.Descripcion ?? mensajeExcel ?? codigo;
            var descripcion = string.IsNullOrWhiteSpace(detalle) ? mensaje : $"{mensaje} — {detalle}";
            if (esError)
            {
                encontrados.Add(new ErrorCpe { Codigo = codigo, Descripcion = descripcion, Ruta = ruta, Origen = origen });
            }
            else
            {
                observaciones.Add(new ObservacionCpe { Codigo = codigo, Descripcion = ruta is null ? descripcion : $"{descripcion} ({ruta})" });
            }
        }
    }

    private void EvaluarReglas(byte[] xml, ReferenciaCpe referencia, Action<string, bool, string?, string, OrigenError, string?> agregar)
    {
        var aplicables = _reglas.Where(r => r.Filas.Any(f => f.Documento == referencia.TipoDocumento)).ToList();
        if (aplicables.Count == 0)
        {
            return;
        }

        DocumentoXml documento;
        try
        {
            documento = DocumentoXml.Cargar(xml, referencia.TipoDocumento);
        }
        catch (XmlException)
        {
            // El XML mal formado ya lo informa el XSD; las reglas no se evalúan.
            return;
        }

        var hoy = reloj.HoyLima();
        foreach (var hallazgo in aplicables.SelectMany(r => r.Evaluar(documento)))
        {
            var fila = excel.Obtener(referencia.TipoDocumento, hallazgo.Fila);
            if (ReglasExcelSunat.TipoVigente(fila, hoy) is { } tipo)
            {
                agregar(fila.Codigo, tipo == "error", hallazgo.Ruta, hallazgo.Detalle, OrigenError.Reglas2027, fila.Mensaje);
            }
        }
    }
}

/// <summary>Errores (rechazos) y observaciones de la validación SUNAT, en orden: XSL y luego reglas complementarias.</summary>
internal sealed record ResultadoValidacionSunat(IReadOnlyList<ErrorCpe> Errores, IReadOnlyList<ObservacionCpe> Observaciones);
