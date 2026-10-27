using System.Text.Json;
using PasheCPE.Tests.Dorados;

namespace PasheCPE.Tests.Utilidades;

/// <summary>
/// Respuestas sintéticas de la API de guías: token y errores con el formato del manual «Servicios web – Plataforma
/// nueva GRE» (§1.2, §1.3); envío y consulta del ticket con el del TRD §17 ([verificar] con el primer envío real).
/// </summary>
internal static class RespuestasGre
{
    public const string Token = "tokenGre";
    public const string Envio = "envioGre";
    public const string Consulta = "consultaGre";

    public static string TokenOk(string token = "tok-1", int expiraEnSegundos = 3600) =>
        JsonSerializer.Serialize(new { access_token = token, token_type = "JWT", expires_in = expiraEnSegundos });

    public static string Ticket(string ticket) =>
        JsonSerializer.Serialize(new { numTicket = ticket, fecRecepcion = "2026-10-22T10:00:01" });

    /// <summary>Estado del ticket: 0, 98 o 99, con el CDR o con el error (numError como número, como lo devuelve SUNAT).</summary>
    public static string Estado(string codigo, byte[]? cdrZip = null, int? numError = null, string? desError = null) =>
        JsonSerializer.Serialize(new
        {
            codRespuesta = codigo,
            error = numError is null ? null : new { numError, desError },
            arcCdr = cdrZip is null ? null : Convert.ToBase64String(cdrZip),
            indCdrGenerado = cdrZip is null ? "0" : "1",
        });

    /// <summary>Error general (400, 401, 500…) o funcional (422 con errors[]).</summary>
    public static string Error(string cod, string msg, params (string Codigo, string Descripcion)[] errores) =>
        JsonSerializer.Serialize(new
        {
            cod,
            msg,
            exc = (string?)null,
            errors = errores.Length == 0 ? null : errores.Select(e => new { codError = e.Codigo, desError = e.Descripcion }).ToArray(),
        });

    /// <summary>CDR dorado real de la GRE T001-31093: aceptada con las observaciones 4391 y 4382 y el enlace del QR.</summary>
    public static byte[] CdrDorado() =>
        File.ReadAllBytes(Path.Combine(LectorDorado.Carpeta("09-GRE"), "R-20611619767-09-T001-31093.zip"));
}
