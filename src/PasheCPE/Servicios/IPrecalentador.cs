namespace PasheCPE;

/// <summary>
/// Compila por adelantado las XSL de SUNAT, los XSD y los serializadores UBL (TRD §6), para que la primera emisión no
/// pague ese costo (≈ 1 s). Con <see cref="PasheCpe.Crear(OpcionesPasheCpe, Microsoft.Extensions.Logging.ILoggerFactory?, TimeProvider?)"/> se ejecuta solo en segundo plano si
/// <see cref="OpcionesPasheCpe.PrecalentarAlIniciar"/>; con <c>AddPasheCpe</c> se llama al iniciar la aplicación:
/// <code>_ = app.Services.GetRequiredService&lt;IPrecalentador&gt;().PrecalentarAsync();</code>
/// Es seguro llamarlo varias veces o en paralelo con las emisiones.
/// </summary>
public interface IPrecalentador
{
    /// <summary>Compila todo lo que aún no esté compilado.</summary>
    Task PrecalentarAsync(CancellationToken ct = default);
}
