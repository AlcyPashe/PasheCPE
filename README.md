# PasheCPE

Librería .NET 10 de **Pashe Soluciones** para la emisión electrónica ante SUNAT desde sistemas de ventas ASP.NET Core.

| Paquete | Contenido |
|---|---|
| `PasheCPE` | UBL 2.1, cálculo tributario, validación local (XSD, XSL SUNAT, reglas 2027), firma, ZIP, QR, envío SOAP a SUNAT u OSE, GRE por API REST, control de respuestas, historial |
| `PasheCPE.Pdf` | Representación impresa A4 (diseño de comprobantes y diseño de guías) |

**Documentos:** Factura (01), Boleta (03), Nota de crédito (07), Nota de débito (08), Resumen diario (RC), Comunicación de baja (RA), Guía remitente (09) y Guía transportista (31).

## Uso rápido

```csharp
builder.Services.AddPasheCpe(builder.Configuration.GetSection("PasheCPE"));
builder.Services.AddPasheCpePdf();

var resultado = await servicioCpe.EmitirAsync(emisor, factura, ct);
switch (resultado.Envio.Estado)
{
    case EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones: /* registrar CDR */ break;
    case EstadoCpe.Rechazado: /* corregir y reemitir */ break;
    case EstadoCpe.SinConexionInternet or EstadoCpe.ServicioNoDisponible: /* reintentar antes de resultado.Envio.FechaLimiteEnvio */ break;
    case EstadoCpe.RespuestaIncierta: /* ConsultarCdrAsync antes de reenviar */ break;
}
```

Guía completa: [`docs/04-Guia-Integracion.md`](docs/04-Guia-Integracion.md).

## Estructura

```
docs\       PRD, TRD, plan, guías
recursos\   XSD, XSL, catálogos y validaciones oficiales de SUNAT
src\        PasheCPE, PasheCPE.Pdf
tests\      pruebas unitarias (sin red) y datos de prueba
samples\    demo de consola para envíos reales (manual)
tools\      generadores de catálogos y referencia UBL
```

## Desarrollo

```powershell
dotnet build PasheCPE.slnx
dotnet test  PasheCPE.slnx
dotnet pack  PasheCPE.slnx -c Release -o artifacts
```

Requisitos: .NET SDK 10.0, PowerShell 7. Paquetes publicados en GitHub Packages (`https://nuget.pkg.github.com/AlcyPashe/index.json`).

© Pashe Soluciones — uso privado.
