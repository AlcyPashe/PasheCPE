# Guía de integración — PasheCPE en un sistema de ventas

Para sistemas ASP.NET Core / Worker Service con SQL Server. La librería no toca la base de datos: el sistema de ventas guarda lo que la librería devuelve.

## 1. Referenciar el paquete

`nuget.config` del sistema de ventas:

```xml
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="pashe" value="https://nuget.pkg.github.com/AlcyPashe/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <pashe>
      <add key="Username" value="AlcyPashe" />
      <add key="ClearTextPassword" value="%PASHE_NUGET_PAT%" />
    </pashe>
  </packageSourceCredentials>
</configuration>
```

`PASHE_NUGET_PAT` es una variable de entorno con un token de GitHub de solo lectura (`read:packages`). Luego:

```powershell
dotnet add package PasheCPE
dotnet add package PasheCPE.Pdf   # opcional
```

## 2. Configuración y registro

```jsonc
"PasheCPE": {
  "RutaBase": "D:\\CPE",
  "Ambiente": "Produccion",
  "ExoneradoAmazoniaPorDefecto": true,
  "PrecalentarAlIniciar": true,
  "Validacion": { "Modelo": true, "Esquema": true, "XslSunat": true, "ReglasComplementarias": true },
  "MontoEnLetras": { "ConTildes": false }
}
```

`Validacion` activa las capas de validación local; conviene dejarlas todas en `true`. `XslSunat` ejecuta las XSL oficiales de SUNAT. `ReglasComplementarias` aplica las reglas del Excel de validación que las XSL no tienen o calculan distinto, incluidas las de 2027. Cada regla entra en vigor según su fecha en los datos de la librería: no hay que cambiar nada el 01/01/2027. `MontoEnLetras.ConTildes` en `false` imprime la leyenda sin tildes, para impresoras térmicas.

```csharp
builder.Services.AddPasheCpe(builder.Configuration.GetSection("PasheCPE"));
builder.Services.AddPasheCpePdf();

var app = builder.Build();
// Compila las XSL de SUNAT, los XSD y los serializadores en segundo plano: la primera emisión no espera ≈ 1 s.
_ = app.Services.GetRequiredService<IPrecalentador>().PrecalentarAsync();
```

Con `PasheCpe.Crear` (sin DI) el precalentamiento se lanza solo si `PrecalentarAlIniciar` es `true`.

Copiar el `.pfx` del cliente a `D:\CPE\CERT\`.

## 3. Construir el emisor desde la BD

```csharp
EmisorCpe emisor = new()
{
    Ruc = empresa.Ruc,
    RazonSocial = empresa.RazonSocial,
    Domicilio = new DireccionCpe { Ubigeo = "160101", Direccion = empresa.Direccion, Distrito = "IQUITOS", Provincia = "MAYNAS", Departamento = "LORETO" },
    Regimen = RegimenTributario.RMT,
    ExoneradoAmazonia = null,                         // usa el valor por defecto (true)
    Canal = CanalEnvio.Sunat,                         // o CanalEnvio.Ose("efact")
    CredencialesSoap = new(empresa.UsuarioSol, Descifrar(empresa.ClaveSol)),
    CredencialesGre = new(empresa.GreClientId, Descifrar(empresa.GreClientSecret), empresa.UsuarioSol, Descifrar(empresa.ClaveSol)),
    Certificado = new("certificado-20601234567.pfx", Descifrar(empresa.ClaveCertificado))
};
```

Las claves se guardan cifradas en el sistema de ventas (por ejemplo, con ASP.NET Core Data Protection) y se descifran solo al construir el emisor.

## 4. Emitir y guardar el resultado

```csharp
ResultadoEmision r = await cpe.EmitirAsync(emisor, factura, ct);

venta.EstadoCpe     = r.Envio.Estado.ToString();
venta.CodigoSunat   = r.Envio.Codigo;
venta.MensajeSunat  = r.Envio.Descripcion;
venta.ValorResumen  = r.Generacion.ValorResumen;
venta.Ticket        = r.Envio.Ticket;
venta.ResultadoJson = JsonSerializer.Serialize(r.Envio, PasheCpeJson.Default.ResultadoEnvio);
```

Mostrar al cajero `r.Envio.MensajeUsuario`.

Las notas de crédito y débito se emiten igual, con una `NotaCpe` que modifica **un solo** comprobante (R.S. 000048-2026). Conviene indicar la `FechaEmision` del documento modificado: con ella la librería valida que la nota no sea anterior y, si generó ese documento, revisa en su historial que esté aceptado, que la moneda coincida y que la NC no supere su importe total.

```csharp
NotaCpe nota = new()
{
    TipoDocumento = TipoDocumentoSunat.NotaDeCredito,
    Serie = "FC01",                                   // F modifica una factura; B, una boleta
    Numero = 15,
    Motivo = MotivoNotaCredito.DevolucionTotal,       // catálogo N.° 09 (N.° 10 en la nota de débito)
    Sustento = "DEVOLUCIÓN TOTAL DE LA MERCADERÍA",
    DocumentoModificado = new() { TipoDocumento = TipoDocumentoSunat.Factura, Serie = "F001", Numero = 1234, FechaEmision = venta.FechaEmision },
    Cliente = cliente,
    Lineas = lineasDevueltas,
};
ResultadoEmision rn = await cpe.EmitirAsync(emisor, nota, ct);
```

Una nota de serie B con `ModoEnvioBoletas = ResumenDiario` solo se genera y se informa después en el resumen diario, como la boleta. La nota de débito por penalidades (motivo 13, desde 2027) puede omitir `DocumentoModificado` y no lleva IGV. La nota de crédito por ajuste de cuotas (motivo 13) lleva importes en cero y la nueva `FormaPago` al crédito.

| Estado | Qué hace el sistema de ventas |
|---|---|
| `Aceptado`, `AceptadoConObservaciones` | Marcar emitido; mostrar observaciones si hay |
| `Rechazado` | Marcar rechazado; corregir y reemitir |
| `ErrorValidacionLocal` | Mostrar `Errores` y corregir antes de reintentar |
| `SinConexionInternet`, `ServicioNoDisponible` | Dejar pendiente; reintentar con `EnviarAsync` antes de `FechaLimiteEnvio` |
| `RespuestaIncierta` | No reenviar; llamar a `ConsultarCdrAsync` |
| `ErrorServicio` | Revisar credenciales, perfil SOL o configuración (alertar al administrador) |
| `EnProceso` | Resumen diario o comunicación de baja: guardar el ticket y consultarlo con `ConsultarTicketAsync` (la referencia es la del RC o la RA). Boleta informada en un resumen: esperar al resultado de su RC |
| `DeBaja` | Marcar anulado: SUNAT aceptó la comunicación de baja que lo informa (§7) |

Tabla sugerida en SQL Server (la crea el sistema de ventas, no la librería):

```sql
CREATE TABLE CpeEnvio (
  Id            BIGINT IDENTITY PRIMARY KEY,
  Ruc           CHAR(11)      NOT NULL,
  TipoDoc       VARCHAR(2)    NOT NULL,
  Serie         VARCHAR(4)    NOT NULL,
  Numero        INT           NOT NULL,
  FechaEmision  DATE          NOT NULL,
  Estado        VARCHAR(30)   NOT NULL,
  Codigo        VARCHAR(10)   NULL,
  Descripcion   NVARCHAR(500) NULL,
  Ticket        VARCHAR(50)   NULL,
  ValorResumen  VARCHAR(100)  NULL,
  ResultadoJson NVARCHAR(MAX) NULL,
  FechaProceso  DATETIMEOFFSET NOT NULL,
  CONSTRAINT UQ_CpeEnvio UNIQUE (Ruc, TipoDoc, Serie, Numero)
);
```

## 5. Sin Internet: generar ahora, enviar después

```csharp
var gen = await cpe.GenerarAsync(emisor, factura, ct);  // funciona sin Internet: imprime con QR y valor resumen
// …más tarde, desde un BackgroundService:
var envio = await cpe.EnviarAsync(emisor, gen.Referencia, ct);
```

Servicio de reenvío sugerido:

```csharp
public sealed class ReenvioCpe(IServicioCpe cpe, IHistorialCpe historial, IEmisores emisores) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (await timer.WaitForNextTickAsync(ct))
        {
            var hoy = DateTime.Today;
            await foreach (var h in historial.ListarPendientesAsync(hoy.Year, hoy.Month, ct))
            {
                var emisor = await emisores.ObtenerAsync(h.Referencia.Ruc, ct);
                var esResumen = h.Referencia.TipoDocumento is ReferenciaCpe.TipoResumenDiario or ReferenciaCpe.TipoComunicacionBaja;
                if (!esResumen && EsBoletaDelResumen(h.Referencia)) // del sistema de ventas: 03 o nota B con ModoEnvioBoletas = ResumenDiario
                {
                    continue; // la informa el resumen diario y se actualiza al consultar su RC
                }

                _ = h.EstadoActual switch
                {
                    EstadoCpe.EnProceso when esResumen => await cpe.ConsultarTicketAsync(emisor, h.Referencia, ct: ct),
                    EstadoCpe.RespuestaIncierta when !esResumen => await cpe.ConsultarCdrAsync(emisor, h.Referencia, ct),
                    _ => await cpe.EnviarAsync(emisor, h.Referencia, ct) // el RC o la RA con ticket se consulta, no se reenvía
                };
                // actualizar la BD con el resultado
            }
        }
    }
}
```

## 6. Boletas

Con `ModoEnvioBoletas = ResumenDiario`: emitir con `GenerarAsync` (se imprime con QR y valor resumen) y, al cierre del día (o al día siguiente, dentro de 7 días), informar las boletas en el resumen diario:

```csharp
// Las líneas se arman con el XML firmado de cada boleta (FIRMA): lo informado coincide con lo firmado.
var documentos = boletasDelDia.Select(b => new DocumentoResumenCpe { TipoDocumento = "03", Serie = b.Serie, Numero = b.Numero }).ToList();
foreach (var rc in await cpe.GenerarResumenAsync(emisor, fecha, documentos, ct))   // un resultado por lote de 500
{
    if (!rc.Exito) { /* errores del modelo o de SUNAT (XSL): corregir y volver a generar */ continue; }
    var envio = await cpe.EnviarAsync(emisor, rc.Referencia, ct);                  // sendSummary → EnProceso con ticket
    var final = await cpe.EsperarTicketAsync(emisor, rc.Referencia, ct: ct);        // 2, 4, 8, 15, 30 s
    // final.Estado: Aceptado / AceptadoConObservaciones / Rechazado, o EnProceso (consultar más tarde)
}
```

- También se puede pasar un `ResumenDiarioCpe` con los totales de cada línea (boletas emitidas por otro sistema).
- Estado del ítem: 1 adicionar, 2 modificar, 3 anular (la anulación de una boleta aceptada se hace con el resumen, dentro de los 7 días).
- La librería asigna los correlativos del día (`RUC-RC-yyyyMMdd-n`) y divide en lotes de 500 líneas.
- El resultado del resumen queda también en el historial de cada boleta: `EnProceso`, luego `Aceptado`; si el resumen se rechaza, las boletas vuelven a `Generado` para incluirlas en otro resumen.
- El ticket queda en el historial del RC: `ConsultarTicketAsync(emisor, referenciaRc)` sin ticket lo toma de ahí, desde otro proceso u otro día.
- Un resumen fuera de plazo se envía con la advertencia `PCPE-1318` (SUNAT lo acepta con la observación 4443).

## 7. Comunicación de baja

Para anular una factura o una nota de serie F **ya aceptada** por SUNAT, dentro de los 7 días siguientes a su emisión (después, se emite una nota de crédito):

```csharp
var baja = new ComunicacionBajaCpe
{
    FechaReferencia = factura.FechaEmision,             // todos los documentos de la RA son de esta fecha
    Documentos = [new DocumentoBajaCpe { TipoDocumento = "01", Serie = "F001", Numero = 125, Motivo = "ERROR EN EL RUC DEL CLIENTE" }],
};
var ra = await cpe.GenerarBajaAsync(emisor, baja, ct);  // RUC-RA-yyyyMMdd-n: DATA, FIRMA, ENVIO y LOG
if (ra.Exito)
{
    var envio = await cpe.EnviarAsync(emisor, ra.Referencia, ct);                  // sendSummary → EnProceso con ticket
    var final = await cpe.EsperarTicketAsync(emisor, ra.Referencia, ct: ct);        // Aceptado → la factura queda DeBaja
}
```

- Las boletas y sus notas no van en la comunicación de baja: se anulan con el estado 3 en el resumen diario (`PCPE-1505`).
- Si PasheCPE generó el documento, la librería revisa su historial: debe estar aceptado (`PCPE-1511`), no dado de baja (`PCPE-1512`) ni en otra RA que aún no concluye (`PCPE-1513`), y su fecha de emisión debe ser la de la RA (`PCPE-1514`).
- Fuera de plazo no se genera ni se envía (`PCPE-1510`); el plazo se vuelve a revisar al enviar.
- Al generar la RA, el historial del documento la anota en `Baja`. Cuando SUNAT la acepta, el documento queda `DeBaja`: ya no se reenvía, no se regenera y no admite notas. Si la RA se rechaza, el documento sigue `Aceptado` y se puede informar en otra RA.
- El ticket queda en el historial de la RA: `ConsultarTicketAsync(emisor, referenciaRa)` lo consulta desde otro proceso u otro día.

## 8. Envío por OSE

El contrato es el mismo: solo cambian el canal y las credenciales del emisor.

```csharp
EmisorCpe emisor = new()
{
    // … mismos datos que en §3
    Canal = CanalEnvio.Ose("efact"),
    CredencialesSoap = new(empresa.Ruc, Descifrar(empresa.ClaveOse)),   // en eFact el usuario es el RUC del emisor
    Certificado = new("certificado-20601234567.pfx", Descifrar(empresa.ClaveCertificado))
};
```

- **eFact viene configurado** para producción (`https://ose.efact.pe/ol-ti-itcpe/billService`). En el mismo servicio están `sendBill`, `sendSummary`, `getStatus` y `getStatusCdr`. Hasta el primer envío real, el log advierte que el endpoint aún no está verificado.
- **eFact no tiene ambiente de pruebas.** Con `Ambiente = Beta` y el canal OSE, el envío devuelve `PCPE-4004` sin llamar a nadie.
- **El usuario se envía tal cual**, sin anteponer el RUC como se hace con SUNAT. En eFact es el RUC del emisor; la clave la asigna eFact.
- **Los mensajes al cajero** dicen «el OSE» en lugar de «SUNAT» (por ejemplo, «Comprobante aceptado por el OSE.»).
- **Otro OSE se agrega por configuración**, con la clave `OSE:{nombre}` y `CanalEnvio.Ose("{nombre}")`:

```json
"PasheCPE": {
  "Endpoints": [
    { "Canal": "OSE:otro", "Ambiente": "Produccion", "Servicio": "Comprobantes", "Url": "https://…/billService", "SoapAction": "urn:{operacion}" },
    { "Canal": "OSE:otro", "Ambiente": "Produccion", "Servicio": "ConsultaCdr", "Url": "https://…/billService", "SoapAction": "urn:{operacion}" }
  ]
}
```

  Sin el endpoint `ConsultaCdr`, el canal no consulta el CDR. Ante una respuesta incierta, se reenvía el mismo ZIP, como en beta.
- **Pendiente de confirmar con el primer cliente:**
  - qué responde eFact cuando la consulta no encuentra el documento. Mientras no se sepa, ese caso no se reenvía y el estado no cambia;
  - qué responde al reenviar un documento que ya recibió.

## 9. Guías de remisión

```csharp
var r = await gre.EmitirAsync(emisor, guia, ct);   // genera, obtiene token, envía, espera ticket y genera el QR de SUNAT
```

Las GRE solo existen en producción. En un sistema configurado con `Ambiente = Beta`, la librería no las envía.

## 10. PDF

```csharp
var pdf = await generadorPdf.GenerarAsync(r.Generacion.Referencia, marca, ct: ct); // REPO\yyyy\MM\{nombre}.pdf
```

`MarcaEmisor` lleva logo, teléfonos, correo y web. El PDF se arma desde el XML firmado, así que puede regenerarse cuando se necesite.

## 11. Diagnóstico

- Historial de cada documento: `D:\CPE\LOG\yyyy\MM\{nombre}.json` o `IHistorialCpe.ObtenerAsync`.
- Logs: categoría `PasheCPE` en el proveedor que use el sistema (Serilog, archivo, consola).
- Conectividad: `IVerificadorConectividad.VerificarAsync(emisor.Canal)` para mostrar en pantalla si SUNAT está disponible.
