# Guía de integración — PasheCPE en un sistema consumidor

Para cualquier sistema .NET 10 que emita comprobantes (ventas, membresías, servicios…), normalmente ASP.NET Core o Worker Service con SQL Server. **La librería no toca la base de datos**: recibe modelos y opciones y devuelve resultados. El sistema consumidor guarda lo que la librería devuelve. El esquema de §4 es solo una referencia.

## 1. Referenciar el paquete

Los paquetes están en GitHub Packages. `nuget.config` del sistema consumidor:

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

`PASHE_NUGET_PAT` es una variable de entorno del equipo de desarrollo y del servidor de compilación, con un token de GitHub de solo lectura (`read:packages`). El token nunca va en el repositorio del sistema consumidor. Luego:

```powershell
dotnet add package PasheCPE --version 1.0.0-rc.1
dotnet add package PasheCPE.Pdf --version 1.0.0-rc.1   # opcional
```

- **Versión fija.** Use siempre una versión exacta, nunca rangos flotantes (`1.0.*`). Las versiones `-rc.N` son preliminares: un sistema en producción pasa de una a otra a propósito, revisando el `CHANGELOG.md`.
- **`PasheCPE.Pdf` y `PasheCPE` van con la misma versión.** `PasheCPE.Pdf` exige la versión exacta de `PasheCPE` con la que se publicó; si no coinciden, la restauración falla.
- **Comprobar un paquete** antes de actualizar un sistema: `pwsh tools/verificar-paquete.ps1 -Version 1.0.0-rc.1 -Origen GitHub`, desde el repositorio de PasheCPE. Crea un proyecto de consola limpio, restaura los dos paquetes con el `nuget.config` de arriba y genera y firma una factura y su PDF sin llamar a SUNAT.

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
builder.Services.AddPasheCpePdf(builder.Configuration.GetSection("PasheCPE:Pdf"));   // opcional (§10)

var app = builder.Build();
// Compila las XSL de SUNAT, los XSD y los serializadores en segundo plano: la primera emisión no espera ≈ 1 s.
_ = app.Services.GetRequiredService<IPrecalentador>().PrecalentarAsync();
```

- Los servicios de la librería (`IServicioCpe`, `IServicioGre`, `IHistorialCpe`, `IGeneradorPdf`…) son **singleton** y seguros para usarse en paralelo. Se inyectan en controladores, servicios scoped o `BackgroundService`.
- La BD del sistema consumidor suele ser **scoped** (por ejemplo, un `DbContext`). Un `BackgroundService` crea un scope por documento (§5).
- Con `PasheCpe.Crear` (sin DI) el precalentamiento se lanza solo si `PrecalentarAlIniciar` es `true`.

Copiar el `.pfx` de cada emisor a `D:\CPE\CERT\` (sin subcarpetas).

## 3. El emisor se construye en cada llamada

Cada operación recibe un `EmisorCpe`. **Se arma desde la BD en cada operación**, justo antes de llamar a la librería, y se descarta después:

```csharp
EmisorCpe emisor = new()
{
    Ruc = empresa.Ruc,
    RazonSocial = empresa.RazonSocial,
    Domicilio = new DireccionCpe { Ubigeo = "160101", Direccion = empresa.Direccion, Distrito = "IQUITOS", Provincia = "MAYNAS", Departamento = "LORETO" },
    Regimen = RegimenTributario.RMT,
    ExoneradoAmazonia = null,                         // usa el valor por defecto (true)
    Canal = CanalEnvio.Sunat,                         // o CanalEnvio.Ose("efact") (§8)
    CredencialesSoap = new(empresa.UsuarioSol, Descifrar(empresa.ClaveSol)),
    CredencialesGre = new(empresa.GreClientId, Descifrar(empresa.GreClientSecret), empresa.UsuarioSol, Descifrar(empresa.ClaveSol)),
    Certificado = new("certificado-20601234567.pfx", Descifrar(empresa.ClaveCertificado))
};
```

- **No se guarda en caché, en un singleton ni en una variable estática.** Así un cambio de clave SOL, de certificado o de canal rige desde la siguiente operación, y las claves descifradas no quedan en memoria más de lo necesario.
- Las claves se guardan **cifradas** en el sistema consumidor (por ejemplo, con ASP.NET Core Data Protection) y se descifran solo aquí.
- **Multiempresa:** un emisor por RUC. La librería solo guarda en caché el certificado cargado (por archivo; lo recarga si cambian el `.pfx` o la clave) y el token de la GRE mientras está vigente. Los archivos de todos los RUC conviven en el mismo almacén porque cada nombre empieza con el RUC.
- En los ejemplos siguientes, `repo.EmisorAsync(ruc, ct)` representa este armado en el repositorio del sistema consumidor.

## 4. Emitir y guardar el resultado

```csharp
var emisor = await repo.EmisorAsync(venta.Ruc, ct);
ResultadoEmision r = await cpe.EmitirAsync(emisor, factura, ct);
await repo.GuardarResultadoAsync(r.Generacion, r.Envio, r.Generacion.Referencia, ct);   // esquema y ejemplo más abajo
```

Mostrar al usuario `r.Envio.MensajeUsuario`.

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

| Estado | Qué hace el sistema consumidor |
|---|---|
| `Generado` | Documento firmado sin enviar (sin Internet, o boleta que espera el resumen diario). Se imprime con QR y valor resumen; lo envía §5 o el resumen de §6 |
| `Aceptado`, `AceptadoConObservaciones` | Marcar emitido; mostrar observaciones si hay |
| `Rechazado` | Marcar rechazado; corregir y reemitir con otro número |
| `ErrorValidacionLocal` | Mostrar `Errores` y corregir antes de reintentar |
| `SinConexionInternet`, `ServicioNoDisponible` | Dejar pendiente; §5 reintenta con `EnviarAsync` antes de `FechaLimiteEnvio` |
| `RespuestaIncierta` | No regenerar. §5 reintenta con `EnviarAsync`, que consulta primero el CDR (`getStatusCdr`) y solo reenvía el mismo ZIP si SUNAT no lo tiene. `ConsultarCdrAsync` solo consulta (en producción) |
| `ErrorServicio` | Revisar credenciales, perfil SOL o configuración (alertar al administrador). No es pendiente |
| `EnProceso` | Resumen diario, comunicación de baja o guía: guardar el ticket; §5 lo consulta con `ConsultarTicketAsync`. Boleta informada en un resumen: espera el resultado de su RC |
| `DeBaja` | Marcar anulado: SUNAT aceptó la comunicación de baja que lo informa (§7) |

### Esquema SQL Server de referencia

Lo crea y mantiene el sistema consumidor; la librería nunca se conecta a una BD. `NombreArchivo` (`ReferenciaCpe.NombreArchivo`, por ejemplo `20601234567-01-F001-125` o `20601234567-RC-20261027-1`) identifica a cada documento en la BD, en el almacén y en el historial de la librería.

```sql
-- Un registro por documento: comprobantes, notas, guías, resúmenes (RC) y comunicaciones de baja (RA).
CREATE TABLE CpeDocumento (
  Id                     BIGINT IDENTITY PRIMARY KEY,
  NombreArchivo          VARCHAR(60)    NOT NULL CONSTRAINT UQ_CpeDocumento UNIQUE,
  Ruc                    CHAR(11)       NOT NULL,
  TipoDoc                VARCHAR(2)     NOT NULL,   -- 01, 03, 07, 08, 09, RC, RA
  Serie                  VARCHAR(4)     NULL,       -- null en RC y RA
  Numero                 BIGINT         NULL,       -- null en RC y RA
  Correlativo            INT            NULL,       -- solo RC y RA
  Fecha                  DATE           NOT NULL,   -- emisión (o generación del RC y la RA)
  Estado                 VARCHAR(30)    NOT NULL,   -- EstadoCpe
  Codigo                 VARCHAR(10)    NULL,
  Descripcion            NVARCHAR(500)  NULL,
  Ticket                 VARCHAR(50)    NULL,
  ValorResumen           VARCHAR(100)   NULL,
  FechaLimiteEnvio       DATE           NULL,
  EsReintentable         BIT            NOT NULL DEFAULT 0,
  RequiereConsultaEstado BIT            NOT NULL DEFAULT 0,
  ReferenciaResumen      VARCHAR(60)    NULL,       -- RC que informa la boleta, o RA que da de baja el documento
  ResultadoJson          NVARCHAR(MAX)  NULL,       -- último ResultadoEnvio completo (PasheCpeJson)
  FechaProceso           DATETIMEOFFSET NULL,       -- del último ResultadoEnvio
  FechaActualizacion     DATETIMEOFFSET NOT NULL
);

-- Pendientes de §5 y boletas sin resumen de §6.
CREATE INDEX IX_CpeDocumento_Pendientes ON CpeDocumento (Ruc, Fecha)
  INCLUDE (TipoDoc, Serie, Numero, Estado, Ticket)
  WHERE Estado IN ('Generado', 'SinConexionInternet', 'ServicioNoDisponible', 'RespuestaIncierta', 'EnProceso');

-- Un registro por intento (ResultadoEnvio.Historial / HistorialCpe.Intentos): auditoría de rechazos y caídas.
CREATE TABLE CpeIntento (
  Id          BIGINT IDENTITY PRIMARY KEY,
  DocumentoId BIGINT         NOT NULL REFERENCES CpeDocumento (Id),
  Fecha       DATETIMEOFFSET NOT NULL,
  Operacion   VARCHAR(30)    NOT NULL,   -- sendBill, getStatusCdr, sendSummary, getStatus, GRE…
  Canal       VARCHAR(30)    NULL,
  Endpoint    VARCHAR(300)   NULL,
  Estado      VARCHAR(30)    NOT NULL,
  Codigo      VARCHAR(10)    NULL,
  Descripcion NVARCHAR(500)  NULL,
  Http        INT            NULL,
  Ticket      VARCHAR(50)    NULL,
  DuracionMs  BIGINT         NOT NULL,
  CONSTRAINT UQ_CpeIntento UNIQUE (DocumentoId, Fecha, Operacion)
);
```

Guardar un resultado: actualizar o insertar el documento y agregar solo los intentos nuevos. `ResultadoEnvio.Historial` trae **todos** los intentos del documento, no solo los de esta llamada:

```csharp
public async Task GuardarResultadoAsync(ResultadoGeneracion? generacion, ResultadoEnvio envio, ReferenciaCpe referencia, CancellationToken ct)
{
    var doc = await db.CpeDocumento.SingleOrDefaultAsync(d => d.NombreArchivo == referencia.NombreArchivo, ct)
              ?? db.CpeDocumento.Add(CpeDocumento.Desde(referencia)).Entity;   // Ruc, TipoDoc, Serie, Numero, Correlativo, Fecha

    doc.Estado                 = envio.Estado.ToString();
    doc.Codigo                 = envio.Codigo;
    doc.Descripcion            = envio.Descripcion;
    doc.Ticket                 = envio.Ticket ?? doc.Ticket;
    doc.ValorResumen           = generacion?.ValorResumen ?? envio.ValorResumen ?? doc.ValorResumen;
    doc.FechaLimiteEnvio       = envio.FechaLimiteEnvio ?? generacion?.FechaLimiteEnvio ?? doc.FechaLimiteEnvio;
    doc.EsReintentable         = envio.EsReintentable;
    doc.RequiereConsultaEstado = envio.RequiereConsultaEstado;
    doc.ResultadoJson          = JsonSerializer.Serialize(envio, PasheCpeJson.Default.ResultadoEnvio);
    doc.FechaProceso           = envio.FechaProceso;
    doc.FechaActualizacion     = reloj.GetUtcNow();

    var ultimo = doc.Intentos.Count == 0 ? DateTimeOffset.MinValue : doc.Intentos.Max(i => i.Fecha);
    foreach (var i in envio.Historial.Where(i => i.Fecha > ultimo))
    {
        doc.Intentos.Add(new CpeIntento
        {
            Fecha = i.Fecha, Operacion = i.Operacion, Canal = i.Canal, Endpoint = i.Endpoint, Estado = i.Estado.ToString(),
            Codigo = i.Codigo, Descripcion = i.Descripcion, Http = i.Http, Ticket = i.Ticket, DuracionMs = i.DuracionMs,
        });
    }

    await db.SaveChangesAsync(ct);
}
```

En la emisión, `referencia` es `r.Generacion.Referencia`. Si la generación falló (`ErrorValidacionLocal`), basta con mostrar los errores: no hay nada que enviar. El ejemplo usa EF Core, pero cualquier acceso a datos sirve.

## 5. Sin Internet: reenvío y reconciliación

```csharp
var gen = await cpe.GenerarAsync(emisor, factura, ct);  // funciona sin Internet: imprime con QR y valor resumen
// …más tarde, desde el BackgroundService de abajo:
var envio = await cpe.EnviarAsync(emisor, gen.Referencia, ct);
```

`ListarPendientesAsync` lee el historial de la librería (`LOG\yyyy\MM`) y devuelve los documentos que aún requieren acción: `Generado` sin enviar, `SinConexionInternet`, `ServicioNoDisponible`, `RespuestaIncierta` y `EnProceso`. `ErrorServicio` no es pendiente, porque lo resuelve el administrador: credenciales, perfil SOL o configuración.

El historial de la librería es la **fuente de verdad** del estado ante SUNAT: se escribe en el mismo momento del envío. La BD puede quedar atrás si el proceso se cae entre el envío y el guardado. Por eso el servicio primero **reconcilia**: si la BD difiere del historial, copia el historial a la BD sin llamar a SUNAT. Solo después decide si envía o consulta.

```csharp
/// <summary>Cada 5 minutos reconcilia la BD con el historial y envía o consulta lo pendiente del mes actual y del anterior.</summary>
public sealed class ReenvioCpe(
    IServicioCpe cpe,
    IServicioGre gre,
    IHistorialCpe historial,
    IServiceScopeFactory scopes,          // la BD del sistema consumidor es scoped; el servicio, singleton
    TimeProvider reloj,
    ILogger<ReenvioCpe> log) : BackgroundService
{
    private static readonly TimeZoneInfo _lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), reloj);
        do
        {
            var hoy = TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), _lima);
            // El mes anterior también: el día 1 aún hay pendientes del último día del mes (la carpeta es la de emisión).
            foreach (var periodo in new[] { hoy.AddMonths(-1), hoy })
            {
                await foreach (var h in historial.ListarPendientesAsync(periodo.Year, periodo.Month, ct))
                {
                    try
                    {
                        await ProcesarAsync(h, ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // La librería no lanza por respuestas de negocio ni de red; esto es del sistema (BD, emisor no encontrado…).
                        log.LogError(ex, "Reenvío de {Documento}", h.Referencia.NombreArchivo);
                    }
                }
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task ProcesarAsync(HistorialCpe h, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioCpe>();
        var r = h.Referencia;

        // 1. Reconciliar: el historial manda. Copia estado, ticket e intentos que la BD no tenga.
        await repo.ReconciliarAsync(h, ct);

        var conTicket = r.TipoDocumento is ReferenciaCpe.TipoResumenDiario or ReferenciaCpe.TipoComunicacionBaja or TipoDocumentoSunat.GuiaDeRemisionRemitente;
        if (!conTicket && h.EstadoActual is EstadoCpe.Generado or EstadoCpe.EnProceso && await repo.EsDelResumenDiarioAsync(r, ct))
        {
            return; // boleta o nota B (ModoEnvioBoletas = ResumenDiario): la informa su RC (§6) y se actualiza al consultarlo
        }

        // 2. Enviar o consultar. El emisor se arma ahora (§3): credenciales descifradas solo aquí.
        var emisor = await repo.EmisorAsync(r.Ruc, ct);
        ResultadoEnvio resultado = (r.TipoDocumento, h.EstadoActual) switch
        {
            (TipoDocumentoSunat.GuiaDeRemisionRemitente, EstadoCpe.EnProceso) => await gre.ConsultarTicketAsync(emisor, r, ct: ct),
            (TipoDocumentoSunat.GuiaDeRemisionRemitente, _) => await gre.EnviarAsync(emisor, r, ct),
            // RC o RA con ticket: nunca se reenvía; sin ticket la consulta lo toma del historial.
            (_, EstadoCpe.EnProceso) when conTicket => await cpe.ConsultarTicketAsync(emisor, r, ct: ct),
            // Comprobantes, y RC o RA aún sin enviar: tras una respuesta incierta, EnviarAsync consulta primero el CDR
            // (getStatusCdr) y solo reenvía si SUNAT no lo tiene; un documento ya aceptado o rechazado no vuelve a enviarse.
            _ => await cpe.EnviarAsync(emisor, r, ct),
        };

        if (resultado.Codigo == "PCPE-4006")
        {
            // Plazo vencido: ya no se envía. Avisar al administrador (factura → nota o baja según el caso).
        }

        await repo.GuardarResultadoAsync(null, resultado, r, ct);   // §4

        // 3. RC o RA concluido: la librería ya actualizó el historial de cada documento informado; llevarlo a la BD.
        if (conTicket && r.TipoDocumento != TipoDocumentoSunat.GuiaDeRemisionRemitente && resultado.Estado != EstadoCpe.EnProceso)
        {
            var informe = await historial.ObtenerAsync(r, ct);
            foreach (var d in informe?.Documentos ?? [])
            {
                if (await historial.ObtenerAsync(d.Referencia, ct) is { } propio)
                {
                    await repo.ReconciliarAsync(propio, ct);   // boleta Aceptado, o Generado si el RC se rechazó; factura DeBaja
                }
            }
        }
    }
}
```

```csharp
builder.Services.AddScoped<IRepositorioCpe, RepositorioCpe>();
builder.Services.AddHostedService<ReenvioCpe>();
```

`IRepositorioCpe` es del sistema consumidor:
- `EmisorAsync` arma el emisor (§3).
- `GuardarResultadoAsync` es el de §4.
- `EsDelResumenDiarioAsync` responde si la serie es B y el sistema trabaja con `ModoEnvioBoletas = ResumenDiario`.
- `ReconciliarAsync(HistorialCpe)` hace lo mismo que `GuardarResultadoAsync`, con datos del historial:
  - `Estado` = `EstadoActual`, más `Ticket`;
  - `Codigo` y `Descripcion` del último intento;
  - agrega a `CpeIntento` los `Intentos` que falten;
  - si `EstadoActual` es `Generado` y antes era `EnProceso` por un RC, limpia `ReferenciaResumen`. Así la boleta vuelve a entrar en el resumen de §6.

Notas:
- **Tickets `EnProceso`.** RC, RA y GRE devuelven un ticket. Si `EsperarTicketAsync` no alcanzó el resultado, el servicio lo consulta en cada vuelta hasta que concluye. La consulta nunca reenvía.
- **`RespuestaIncierta`.** No se regenera ni se cambia el número. `EnviarAsync` consulta primero el CDR. Donde no se puede consultar (beta, boletas, series B, un OSE sin `ConsultaCdr`), reenvía **el mismo ZIP**.
- Varios procesos pueden reenviar a la vez sin duplicar un `sendBill`: la librería serializa los envíos del mismo documento.
- Si el sistema ya guarda los pendientes en su BD, puede recorrer `CpeDocumento` con el índice de pendientes en lugar del historial. Las llamadas son las mismas, pero conviene reconciliar con `IHistorialCpe.ObtenerAsync` antes de cada envío.
- `EnviarAsync` revisa el plazo antes de enviar y nunca reenvía a ciegas.

## 6. Boletas y resumen diario programado

Con `ModoEnvioBoletas = ResumenDiario`: emitir con `GenerarAsync` (se imprime con QR y valor resumen) y, al cierre del día (o después, antes de su `FechaLimiteEnvio`), informar las boletas en el resumen diario:

```csharp
// Las líneas se arman con el XML firmado de cada boleta (FIRMA): lo informado coincide con lo firmado.
var documentos = boletasDelDia.Select(b => new DocumentoResumenCpe { TipoDocumento = "03", Serie = b.Serie, Numero = b.Numero }).ToList();
foreach (var rc in await cpe.GenerarResumenAsync(emisor, fecha, documentos, ct))   // un resultado por lote de 500
{
    if (!rc.Exito) { /* errores del modelo o de SUNAT (XSL): corregir y volver a generar */ continue; }
    var envio = await cpe.EnviarAsync(emisor, rc.Referencia, ct);                  // sendSummary → EnProceso con ticket
    var final = await cpe.EsperarTicketAsync(emisor, rc.Referencia, ct: ct);        // 2, 4, 8, 15, 30 s
    // final.Estado: Aceptado / AceptadoConObservaciones / Rechazado, o EnProceso (lo consulta §5)
}
```

- También se puede pasar un `ResumenDiarioCpe` con los totales de cada línea (boletas emitidas por otro sistema).
- Estado del ítem: 1 adicionar, 2 modificar, 3 anular (la anulación de una boleta aceptada se hace con el resumen, dentro del plazo del resumen diario).
- **Plazos:** la librería los toma de `plazos.json` y no se escriben en el sistema consumidor (TRD §27.2). Cada boleta trae su último día en `ResultadoGeneracion.FechaLimiteEnvio`, que §4 guarda en `CpeDocumento.FechaLimiteEnvio`.
- La librería asigna los correlativos del día (`RUC-RC-yyyyMMdd-n`) y divide en lotes de 500 líneas.
- El resultado del resumen queda también en el historial de cada boleta: `EnProceso`, luego `Aceptado`; si el resumen se rechaza, las boletas vuelven a `Generado` para incluirlas en otro resumen.
- El ticket queda en el historial del RC: `ConsultarTicketAsync(emisor, referenciaRc)` sin ticket lo toma de ahí, desde otro proceso u otro día.
- Un resumen fuera de plazo se envía con la advertencia `PCPE-1318` (SUNAT lo acepta con la observación 4443).

### Resumen programado

Un `BackgroundService` que cada día, a una hora fija de Lima, informa las boletas y notas B de días anteriores que siguen sin resumen. Recupera los días atrasados (sistema apagado, sin Internet). El plazo no se escribe en el servicio: cada boleta lo trae en `FechaLimiteEnvio`, guardado al generarla (§4):

```csharp
/// <summary>Cada día a las 02:00 de Lima envía el resumen diario de las boletas de días anteriores que siguen sin resumen.</summary>
public sealed class ResumenDiarioProgramado(
    IServicioCpe cpe,
    IServiceScopeFactory scopes,
    TimeProvider reloj,
    ILogger<ResumenDiarioProgramado> log) : BackgroundService
{
    private static readonly TimeZoneInfo _lima = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
    private static readonly TimeSpan _hora = new(2, 0, 0);   // configurable

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ahora = TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), _lima);
            var siguiente = ahora.Date + _hora;
            if (siguiente <= ahora) siguiente = siguiente.AddDays(1);
            await Task.Delay(siguiente - ahora, reloj, ct);

            var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), _lima).DateTime);
            try
            {
                await InformarAsync(hoy, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Resumen diario programado del {Fecha}", hoy);
            }
        }
    }

    private async Task InformarAsync(DateOnly hoy, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioCpe>();

        // Boletas y notas B en Generado, sin ReferenciaResumen, emitidas hasta ayer; agrupadas por RUC y fecha.
        foreach (var grupo in await repo.ListarSinResumenAsync(hasta: hoy.AddDays(-1), ct))
        {
            if (grupo.Documentos.Any(d => d.FechaLimiteEnvio < hoy))
            {
                // Plazo de plazos.json vencido: la librería lo envía igual con la advertencia PCPE-1318 (SUNAT lo acepta
                // con la observación 4443). Avisar al administrador.
                log.LogWarning("Resumen {Fecha} de {Ruc} fuera de plazo", grupo.Fecha, grupo.Ruc);
            }

            var emisor = await repo.EmisorAsync(grupo.Ruc, ct);   // §3
            var documentos = grupo.Documentos.Select(d => new DocumentoResumenCpe { TipoDocumento = d.TipoDoc, Serie = d.Serie, Numero = d.Numero }).ToList();

            foreach (var rc in await cpe.GenerarResumenAsync(emisor, grupo.Fecha, documentos, ct))
            {
                if (!rc.Exito)
                {
                    log.LogWarning("Resumen {Fecha} de {Ruc}: {Errores}", grupo.Fecha, grupo.Ruc, string.Join("; ", rc.Errores.Select(e => $"{e.Codigo} {e.Descripcion}")));
                    continue;
                }

                // Marca las boletas del lote con este RC antes de enviarlo: no se vuelven a informar en otra vuelta.
                await repo.AsignarResumenAsync(rc.Referencia, ct);
                var envio = await cpe.EnviarAsync(emisor, rc.Referencia, ct);   // EnProceso con ticket; §5 lo consulta
                await repo.GuardarResultadoAsync(rc, envio, rc.Referencia, ct);
            }
        }
    }
}
```

- `AsignarResumenAsync` lee las boletas del lote en `IHistorialCpe.ObtenerAsync(rc.Referencia)` (`Documentos`) y les pone `ReferenciaResumen`.
- Si el envío falla (sin Internet), el RC queda `Generado` en el historial y §5 lo envía. Nunca se genera otro RC para las mismas boletas mientras el primero no concluya.
- El servicio no espera el ticket: §5 lo consulta y reconcilia las boletas.
- La hora de envío es decisión de cada sistema: de madrugada no compite con las ventas.

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
- **Los mensajes al usuario** dicen «el OSE» en lugar de «SUNAT» (por ejemplo, «Comprobante aceptado por el OSE.»).
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

La guía remitente (09) va por la API REST de SUNAT, que **solo existe en producción**: toda guía enviada es oficial.

```csharp
EmisorCpe emisor = /* … como en §3 */ with
{
    CredencialesGre = new(empresa.GreClientId, Descifrar(empresa.GreClientSecret), empresa.UsuarioSol, Descifrar(empresa.ClaveSol)),
};
var guia = new GuiaRemisionCpe
{
    Serie = "T001", Numero = 125,
    Destinatario = new ClienteCpe { TipoDocumento = TipoDocumentoIdentidad.Ruc, NumeroDocumento = "20611619767", RazonSocial = "CLIENTE S.A.C." },
    Motivo = MotivoTraslado.Venta,
    Modalidad = ModalidadTraslado.TransportePublico,
    FechaInicioTraslado = hoy,
    PesoBruto = 12.5m,                                            // KGM por defecto (o TNE)
    Partida = new PuntoTrasladoCpe { Ubigeo = "160101", Direccion = "AV. AUGUSTO FREYRE 727" },
    Llegada = new PuntoTrasladoCpe { Ubigeo = "160101", Direccion = "CAL CALVO 1004" },
    Transportista = new TransportistaCpe { Ruc = "20607254720", RazonSocial = "TRANSPORTES S.A.C.", RegistroMtc = "0001234CNG" },
    DocumentosRelacionados = [new DocumentoTrasladoCpe { Tipo = DocumentoRelacionadoTraslado.Factura, Numero = "F001-125" }],
    Bienes = [new BienGuiaCpe { Descripcion = "BEBIDA DE ALMENDRA 946ML", Cantidad = 3 }],
};
var r = await pashe.Gre.EmitirAsync(emisor, guia, ct);   // genera, pide o reutiliza el token, envía, espera el ticket y genera el QR
```

- **Modalidad privada:** en lugar del transportista, `Vehiculos` (el primero es el principal) y `Conductores` (el primero es el principal), hasta 2 secundarios de cada uno. Con `Indicadores.VehiculoM1L` no se piden.
- **Validación local:** el modelo (`PCPE-16xx`), el XSD y la XSL GRE 2.0 de SUNAT (01/10/2026). Las observaciones de SUNAT, como la 4391 sin registro MTC del transportista, llegan en las advertencias.
- **Plazo:** SUNAT recibe la guía hasta 1 día después de la emisión (`PCPE-1604`, `PCPE-4006`).
- **Token:** se pide con el `client_id`, el `client_secret` y el usuario y la clave SOL (menú SOL → Credenciales de API SUNAT). Dura 1 hora y lo reutilizan todos los envíos y consultas de ese periodo; vencido o rechazado (401), la librería pide otro.
- **Ticket:** `EnviarAsync` devuelve `EnProceso` con el ticket; `ConsultarTicketAsync` o `EsperarTicketAsync` traen el CDR. Al aceptarse, `ContenidoQr` es el enlace de SUNAT y el PNG queda en `QR\yyyy\MM`.
- **Ambiente:** con `Ambiente = Beta` la librería no envía guías (`PCPE-4602`), salvo `PasheCPE:Gre:PermitirEnvioConAmbienteBeta = true`. Aunque el emisor envíe sus comprobantes por OSE, la guía va a SUNAT.
- **Pendiente de confirmar con el primer envío real:** el JSON del envío y de la consulta, el formato de `hashZip` y la respuesta a un reenvío.

## 10. PDF

Representación impresa A4 de facturas, boletas, notas de crédito y débito y guías remitente. El PDF se arma desde el XML firmado de `FIRMA` y el CDR de `RPTA`, así que puede regenerarse cuando se necesite. Queda en `REPO\yyyy\MM\{nombre}.pdf`.

```csharp
var marca = new MarcaEmisor
{
    RutaLogo = @"D:\CPE\logo.png",            // o Logo = bytes (PNG o JPEG)
    ColorPrincipal = "#065986",
    Telefonos = ["(065) 123456"],
    Correo = "ventas@empresa.pe",
    Web = "www.empresa.pe",
};

var pdf = await generadorPdf.GenerarAsync(r.Generacion.Referencia, marca, ct);   // IGeneradorPdf
if (!pdf.Exito)
{
    // pdf.Errores[0].Codigo: PCPE-5002 (no se generó el XML), 5005 (guía aún no aceptada)…
}
```

**Automático.** Con `"GenerarPdfAutomaticamente": true`, `EmitirAsync` deja el PDF cuando el documento queda aceptado, y también en las boletas que esperan el resumen diario. Lo indica en `r.Envio.Rutas.Pdf`. Usa la marca registrada para el RUC del emisor:

```jsonc
"PasheCPE": {
  "GenerarPdfAutomaticamente": true,
  "Pdf": {
    "MarcaPorDefecto": { "RutaLogo": "D:\\CPE\\logo.png", "ColorPrincipal": "#065986" },
    "Marcas": { "20493400364": { "Web": "www.jully.pe", "Telefonos": [ "(065) 123456" ] } }
  }
}
```

Si el PDF falla, la venta no se afecta: la emisión trae la advertencia `PCPE-5001` en `r.Generacion.Advertencias` y el PDF se puede generar después con `GenerarAsync`.

Sin DI: `using var pashe = PasheCpePdf.Crear(opciones, opcionesPdf);` y `pashe.Pdf().GenerarAsync(...)`.

## 11. Diagnóstico

- Historial de cada documento: `D:\CPE\LOG\yyyy\MM\{nombre}.json` o `IHistorialCpe.ObtenerAsync`.
- Logs: categoría `PasheCPE` en el proveedor que use el sistema (Serilog, archivo, consola).
- Conectividad: `IVerificadorConectividad.VerificarAsync(emisor.Canal)` para mostrar en pantalla si SUNAT está disponible.
- Trazas y métricas con el nombre `PasheCPE`. Con OpenTelemetry se registran con `.WithTracing(t => t.AddSource("PasheCPE"))` y `.WithMetrics(m => m.AddMeter("PasheCPE"))`.
  - Actividades: `generar` (con `validar` y `firmar` dentro), `sendBill`, `getStatusCdr`, `sendSummary`, `getStatus` y las de la GRE. Llevan el nombre del archivo en `pashecpe.documento`.
  - Métricas: `pashecpe.envios` (por operación y estado), `pashecpe.envio.duracion` (ms) y `pashecpe.validacion.errores` (por tipo de documento y origen: `Modelo`, `Esquema`, `XslSunat`, `ReglasComplementarias`).
  - Ni los logs ni las trazas llevan credenciales. En `Debug` se ven los sobres SOAP y el JSON de la GRE con la clave, el token y el Base64 enmascarados.
- El resultado del PDF se guarda como JSON con `PasheCpePdfJson.Default.ResultadoPdf`.
