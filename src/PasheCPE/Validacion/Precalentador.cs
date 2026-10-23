using PasheCPE.Serializacion;
using PasheCPE.Ubl;

namespace PasheCPE.Validacion;

/// <summary>Compila en paralelo las XSL de SUNAT, el conjunto de XSD y los serializadores UBL.</summary>
internal sealed class Precalentador(ValidadorEsquema esquema) : IPrecalentador
{
    public Task PrecalentarAsync(CancellationToken ct = default)
    {
        IEnumerable<Action> tareas =
        [
            esquema.Precalentar,
            SerializadorUbl.Precalentar<Invoice>,
            .. MotorXslSunat.XslEmbebidas.Select(archivo => (Action)(() => MotorXslSunat.Compilar(archivo))),
        ];
        return Parallel.ForEachAsync(tareas, ct, (tarea, _) =>
        {
            tarea();
            return ValueTask.CompletedTask;
        });
    }
}
