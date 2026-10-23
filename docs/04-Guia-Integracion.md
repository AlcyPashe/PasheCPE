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
  "PrecalentarAlIniciar": true
}
```

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

| Estado | Qué hace el sistema de ventas |
|---|---|
| `Aceptado`, `AceptadoConObservaciones` | Marcar emitido; mostrar observaciones si hay |
| `Rechazado` | Marcar rechazado; corregir y reemitir |
| `ErrorValidacionLocal` | Mostrar `Errores` y corregir antes de reintentar |
| `SinConexionInternet`, `ServicioNoDisponible` | Dejar pendiente; reintentar con `EnviarAsync` antes de `FechaLimiteEnvio` |
| `RespuestaIncierta` | No reenviar; llamar a `ConsultarCdrAsync` |
| `ErrorServicio` | Revisar credenciales, perfil SOL o configuración (alertar al administrador) |
| `EnProceso` | Guardar el ticket; consultar con `ConsultarTicketAsync` |

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
                _ = h.EstadoActual switch
                {
                    EstadoCpe.EnProceso         => await cpe.ConsultarTicketAsync(emisor, h.Referencia, h.Ticket!, ct),
                    EstadoCpe.RespuestaIncierta => await cpe.ConsultarCdrAsync(emisor, h.Referencia, ct),
                    _                           => await cpe.EnviarAsync(emisor, h.Referencia, ct)
                };
                // actualizar la BD con el resultado
            }
        }
    }
}
```

## 6. Boletas

Con `ModoEnvioBoletas = ResumenDiario`: emitir con `GenerarAsync` y, al cierre del día (o al día siguiente, dentro de 7 días), enviar `GenerarResumenAsync` + `EnviarAsync` + `EsperarTicketAsync`. La librería arma lotes de 500 y asigna correlativos.

## 7. Guías de remisión

```csharp
var r = await gre.EmitirAsync(emisor, guia, ct);   // genera, obtiene token, envía, espera ticket y genera el QR de SUNAT
```

Las GRE solo existen en producción. En un sistema configurado con `Ambiente = Beta`, la librería no las envía.

## 8. PDF

```csharp
var pdf = await generadorPdf.GenerarAsync(r.Generacion.Referencia, marca, ct: ct); // REPO\yyyy\MM\{nombre}.pdf
```

`MarcaEmisor` lleva logo, teléfonos, correo y web. El PDF se arma desde el XML firmado, así que puede regenerarse cuando se necesite.

## 9. Diagnóstico

- Historial de cada documento: `D:\CPE\LOG\yyyy\MM\{nombre}.json` o `IHistorialCpe.ObtenerAsync`.
- Logs: categoría `PasheCPE` en el proveedor que use el sistema (Serilog, archivo, consola).
- Conectividad: `IVerificadorConectividad.VerificarAsync(emisor.Canal)` para mostrar en pantalla si SUNAT está disponible.
