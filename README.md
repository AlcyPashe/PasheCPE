# PasheCPE

Librería .NET 10 de **Pashe Soluciones** para la emisión electrónica ante SUNAT desde sistemas de ventas ASP.NET Core.

| Paquete | Contenido |
|---|---|
| `PasheCPE` | UBL 2.1, cálculo tributario, validación local (XSD, XSL SUNAT, reglas complementarias del Excel, incluidas las de 2027), firma, ZIP, QR, envío SOAP a SUNAT u OSE, GRE por API REST, control de respuestas, historial |
| `PasheCPE.Pdf` | Representación impresa A4 de comprobantes y guías remitente, a pedido o automática al aceptarse |

**Documentos:** Factura (01), Boleta (03), Nota de crédito (07), Nota de débito (08), Resumen diario (RC), Comunicación de baja (RA) y Guía remitente (09). La guía transportista (31) está prevista para una versión posterior.

## Instalación

Desde GitHub Packages, con versión fija y el `nuget.config` de la [guía §1](docs/04-Guia-Integracion.md#1-referenciar-el-paquete):

```powershell
dotnet add package PasheCPE --version 1.0.0-rc.1
dotnet add package PasheCPE.Pdf --version 1.0.0-rc.1   # opcional, misma versión que PasheCPE
```

## Uso rápido

```csharp
builder.Services.AddPasheCpe(builder.Configuration.GetSection("PasheCPE"));
builder.Services.AddPasheCpePdf(builder.Configuration.GetSection("PasheCPE:Pdf"));   // opcional: marcas por RUC

var resultado = await servicioCpe.EmitirAsync(emisor, factura, ct);
switch (resultado.Envio.Estado)
{
    case EstadoCpe.Aceptado or EstadoCpe.AceptadoConObservaciones: /* registrar CDR */ break;
    case EstadoCpe.Rechazado: /* corregir y reemitir */ break;
    case EstadoCpe.SinConexionInternet or EstadoCpe.ServicioNoDisponible: /* reintentar antes de resultado.Envio.FechaLimiteEnvio */ break;
    case EstadoCpe.RespuestaIncierta: /* EnviarAsync más tarde: consulta el CDR antes de reenviar */ break;
}
```

Con `"GenerarPdfAutomaticamente": true` y `PasheCPE.Pdf` registrado, el PDF de cada documento aceptado queda en `resultado.Envio.Rutas.Pdf`. Los resultados se guardan como JSON con `PasheCpeJson.Default`.

Guía completa: [`docs/04-Guia-Integracion.md`](docs/04-Guia-Integracion.md).

> **Contrato estable:** los modelos (`EmisorCpe`, `ComprobanteCpe`…), los resultados, las opciones, las interfaces y las constantes de catálogo. Las clases del namespace `PasheCPE.Ubl` son públicas solo porque `XmlSerializer` lo exige: **no forman parte del contrato estable** y pueden cambiar en versiones menores sin aviso.

## Estructura

```
docs\       PRD, TRD, plan, guías
recursos\   XSD, XSL, catálogos y validaciones oficiales de SUNAT
src\        PasheCPE, PasheCPE.Pdf
tests\      pruebas unitarias (sin red) y datos de prueba
samples\    demo de consola para envíos reales (manual)
benchmarks\ mediciones de rendimiento contra las metas del TRD §20 (BenchmarkDotNet, no se publica)
tools\      generadores de catálogos y referencia UBL
```

## Desarrollo

```powershell
dotnet build PasheCPE.slnx
dotnet test  PasheCPE.slnx
dotnet pack  PasheCPE.slnx -c Release -o artifacts
dotnet run -c Release --project benchmarks/PasheCPE.Benchmarks -- --filter "*"   # rendimiento (manual)
```

Requisitos: .NET SDK 10.0, PowerShell 7. Paquetes publicados en GitHub Packages (`https://nuget.pkg.github.com/AlcyPashe/index.json`).

© Pashe Soluciones — uso privado.
