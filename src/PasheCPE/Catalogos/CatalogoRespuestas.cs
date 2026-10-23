using System.Collections.Frozen;

namespace PasheCPE.Catalogos;

/// <summary>
/// Datos para interpretar las respuestas: estados de <c>getStatusCdr</c> (<c>estados-consulta-cdr.json</c>), del ticket
/// de <c>getStatus</c> (<c>estados-ticket.json</c>) y el mensaje en español claro para el cajero
/// (<c>mensajes-usuario.json</c>).
/// </summary>
internal sealed class CatalogoRespuestas
{
    private readonly Lazy<FrozenDictionary<string, EstadoConsultaCdr>> _estadosConsulta = new(() =>
        RecursosEmbebidos.Leer("estados-consulta-cdr.json", DatosJson.Default.ArchivoEstadosConsultaCdr).Estados
            .ToFrozenDictionary(e => e.Codigo, StringComparer.Ordinal));

    private readonly Lazy<FrozenDictionary<string, EstadoTicket>> _estadosTicket = new(() =>
        RecursosEmbebidos.Leer("estados-ticket.json", DatosJson.Default.ArchivoEstadosTicket).Estados
            .ToFrozenDictionary(e => e.Codigo, StringComparer.Ordinal));

    private readonly Lazy<ArchivoMensajesUsuario> _mensajes = new(() =>
        RecursosEmbebidos.Leer("mensajes-usuario.json", DatosJson.Default.ArchivoMensajesUsuario));

    /// <summary>Estado de la consulta del CDR; acepta «1» o «0001».</summary>
    public EstadoConsultaCdr? EstadoConsulta(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? null : _estadosConsulta.Value.GetValueOrDefault(codigo.Trim().PadLeft(4, '0'));

    /// <summary>Estado del ticket de getStatus (0, 98, 99); acepta ceros a la izquierda.</summary>
    public EstadoTicket? EstadoTicket(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var normalizado = codigo.Trim().TrimStart('0');
        return _estadosTicket.Value.GetValueOrDefault(normalizado.Length == 0 ? "0" : normalizado);
    }

    /// <summary>Mensaje del código si tiene uno propio; si no, el del estado.</summary>
    public string MensajeUsuario(EstadoCpe estado, string? codigo)
    {
        var mensajes = _mensajes.Value;
        if (codigo is not null && mensajes.Codigos.TryGetValue(codigo, out var porCodigo))
        {
            return porCodigo;
        }

        return mensajes.Estados.TryGetValue(estado.ToString(), out var porEstado) ? porEstado : string.Empty;
    }
}
