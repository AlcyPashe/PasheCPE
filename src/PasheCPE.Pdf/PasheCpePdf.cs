using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PasheCPE.Pdf;

/// <summary>
/// Uso de PasheCPE con PDF sin inyección de dependencias: el mismo contenedor que <see cref="PasheCpe.Crear(OpcionesPasheCpe, ILoggerFactory?, TimeProvider?)"/>
/// más el generador de PDF.
/// </summary>
/// <example>
/// <code>
/// using var pashe = PasheCpePdf.Crear(opciones, new OpcionesPasheCpePdf { MarcaPorDefecto = marca });
/// var pdf = await pashe.Pdf().GenerarAsync(referencia, marca);
/// </code>
/// </example>
public static class PasheCpePdf
{
    /// <summary>Crea la instancia con PDF; las opciones se validan de inmediato como en <c>PasheCpe.Crear</c>.</summary>
    public static PasheCpe Crear(OpcionesPasheCpe opciones, OpcionesPasheCpePdf? opcionesPdf = null, ILoggerFactory? fabricaRegistros = null, TimeProvider? tiempo = null) =>
        PasheCpe.Crear(opciones, fabricaRegistros, tiempo, servicios =>
        {
            servicios.AddSingleton(Options.Create(opcionesPdf ?? new OpcionesPasheCpePdf()));
            servicios.AgregarPdf();
        });

    /// <summary>Generador de PDF de una instancia creada con <see cref="Crear"/>.</summary>
    public static IGeneradorPdf Pdf(this PasheCpe pashe)
    {
        ArgumentNullException.ThrowIfNull(pashe);
        return pashe.Servicios.GetService<IGeneradorPdf>()
            ?? throw new InvalidOperationException("La instancia no tiene PDF: créela con PasheCpePdf.Crear.");
    }
}
