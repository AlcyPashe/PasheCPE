# Contrato de Pashe.Cpe requerido por Bottega

> Copia de las secciones 13 y 14 del TRD de Bottega (09/10/2026). Describe lo **mínimo** que Bottega necesita de Pashe.Cpe. Los documentos propios de Pashe.Cpe (PRD, TRD y plan) prevalecen para el diseño interno; si algo aquí difiere de ellos, se señala y se decide antes de codificar.

## 13. Pashe.Cpe — librería de comprobantes electrónicos

### 13.1 Principios
- `net10.0`, en `D:\Sistemas\Pashe.Cpe\`, con su propia solución, `CLAUDE.md` y pruebas.
- Sin componentes de terceros para CPE; dependencias permitidas: `System.Security.Cryptography.Xml` y `QRCoder`.
- No conoce Bottega ni base de datos. Devuelve objetos de resultado; los errores de red se devuelven como `ErrorComunicacion`.
- Orden de construcción: **1) `Invoice.cs`** (Factura 01 y Boleta 03), **2) `Summary.cs`**. Luego `CreditNote.cs`, `DebitNote.cs`, `Voided.cs`.

### 13.2 Estructura
```
Pashe.Cpe\
├── src\Pashe.Cpe\
│   ├── Modelos\        DocumentoCpe, EmisorCpe, ReceptorCpe, LineaCpe, TotalesCpe, LeyendaCpe,
│   │                   ResumenDiarioCpe, LineaResumenCpe, OpcionesCpe
│   ├── Ubl\Comunes\    Componentes cbc/cac/ext reutilizables
│   ├── Ubl\Documentos\ Invoice.cs, Summary.cs
│   ├── Mapeo\          MapeadorInvoice, MapeadorSummary
│   ├── Serializacion\  SerializadorUbl (UTF-8 sin BOM)
│   ├── Firma\          FirmadorXml (devuelve DigestValue)
│   ├── Empaquetado\    EmpaquetadorZip
│   ├── Sunat\          ClienteSunatSoap, ConstructorSobreSoap, LectorCdr, ClasificadorCodigoSunat, Resultados\
│   ├── Almacenamiento\ AlmacenCpe (7 carpetas, subcarpetas yyyy\MM)
│   ├── Qr\             ConstructorContenidoQr, GeneradorQrPng
│   ├── Utilidades\     NumeroALetras, NombreArchivoCpe, Redondeo
│   ├── Catalogos\
├── recursos\XSD    Archivos XSD de SUNAT para la generación de las clases Invoice.cs, Summary.cs y demás.
├── recursos\XSL    Archivos XSL de SUNAT para las validaciones.
└── tests\Pashe.Cpe.Tests\
```

### 13.3 Contrato público mínimo
```csharp
public sealed class ProcesadorCpe
{
    public ProcesadorCpe(OpcionesCpe opciones, HttpClient http);
    public ResultadoGeneracion GenerarFactura(DocumentoCpe doc);       // también Boleta (03)
    public ResultadoGeneracion GenerarResumen(ResumenDiarioCpe res);
    public Task<ResultadoEnvio>  EnviarComprobanteAsync(string nombreArchivo, DateOnly fechaEmision, CancellationToken ct);
    public Task<ResultadoEnvio>  EnviarResumenAsync(string nombreArchivo, DateOnly fechaGeneracion, CancellationToken ct);
    public Task<ResultadoTicket> ConsultarTicketAsync(string ticket, string nombreArchivo, DateOnly fecha, CancellationToken ct);
    public Task<ResultadoEnvio>  ConsultarCdrAsync(string ruc, string tipo, string serie, int numero, DateOnly fechaEmision, CancellationToken ct);
}

public sealed record OpcionesCpe(
    string RutaBase, bool OrganizarPorPeriodo,
    string RutaCertificado, string PasswordCertificado,
    string UrlEnvio, string? UrlConsultaCdr,
    string Ruc, string UsuarioSol, string ClaveSol, int TimeoutSegundos);
```
La fecha se pasa para ubicar la subcarpeta `yyyy\MM`. `ResultadoGeneracion` y `ResultadoEnvio` como en v1.0 (éxito, nombre de archivo, valor resumen, contenido QR, rutas, códigos, notas, ticket, fault, HTTP, CDR, excepción, tiempos).

### 13.4 Detalles UBL
- **Invoice 2.1**: `UBLVersionID 2.1`, `CustomizationID 2.0`, `InvoiceTypeCode` 01/03 con `listID="0101"`; emisor con establecimiento; receptor; `PaymentTerms` (Contado) en facturas; `TaxTotal` por tributo (1000, 9997, 9998, 7152); `LegalMonetaryTotal`.
- **Por línea**: `InvoicedQuantity` con `unitCode` de la presentación; `PricingReference` (precio con impuestos, tipo 01); `TaxTotal` de línea; `cac:Item/cbc:Description`; **código de producto SUNAT** en `cac:CommodityClassification/cbc:ItemClassificationCode listID="UNSPSC"`; **GTIN** en `cac:StandardItemIdentification/cbc:ID` con `schemeID` según su longitud (`GTIN-8`, `GTIN-12`, `GTIN-13`, `GTIN-14`), según la guía UBL vigente de SUNAT.
- **Summary**: `UBLVersionID 2.0`, `CustomizationID 1.1`, `RC-yyyyMMdd-n`, `ReferenceDate`, líneas con `ConditionCode` 1/2/3.
- **Firma**: XML-DSig *enveloped*, RSA-SHA1/SHA1 (confirmar con la documentación de Pashe.Cpe), C14N, en `ext:UBLExtensions`. Certificado con `X509CertificateLoader.LoadPkcs12FromFile`.
- **Nombres**: `RUC-TT-SERIE-NUMERO`, `RUC-RC-yyyyMMdd-n`, CDR `R-<nombre>.zip`.
- **SOAP** manual 1.1 con `wsse:UsernameToken`; `sendBill`, `sendSummary`, `getStatus`, `getStatusCdr`.

### 13.5 Clasificación de resultados → estados

| Situación | Resultado | Estado en Bottega |
|---|---|---|
| CDR 0 sin notas | Aceptado | ACEPTADO |
| CDR 0 con notas ≥ 4000 | AceptadoConObservaciones | ACEPTADO_OBS |
| Código 2000–3999 | Rechazado | RECHAZADO (habilita reemisión) |
| Fault 0100–1999 (salvo 1033) | Excepcion | EXCEPCION |
| Fault 1033 | Excepcion | consulta `getStatusCdr` (producción) |
| Timeout, sin red, HTTP 5xx | ErrorComunicacion | sin cambio |
| `sendSummary` correcto | EnProceso | resumen y boletas EN_PROCESO |
| `getStatus` 98 | EnProceso | sin cambio |
| `getStatus` 0 | según CDR | boletas ACEPTADO/ACEPTADO_OBS |
| `getStatus` 99 | Rechazado | resumen RECHAZADO; boletas vuelven a FIRMADO (o RECHAZADO si es por plazo) |

---

## 14. Código QR (RN-45)

| # | Campo | Formato | Ejemplo |
|---|---|---|---|
| 1 | RUC emisor | 11 dígitos | `10456789123` |
| 2 | Tipo de documento | catálogo 01 | `03` |
| 3 | Serie | 4 caracteres | `B001` |
| 4 | Número | sin ceros a la izquierda | `125` |
| 5 | Monto total IGV | `0.00` | `0.00` |
| 6 | Monto total | `0.00` | `25.50` |
| 7 | Fecha de emisión | `yyyy-MM-dd` | `2026-10-09` |
| 8 | Tipo doc. adquirente | catálogo 06 | `1` |
| 9 | Número doc. adquirente | tal cual | `45678912` |
| 10 | Valor resumen | DigestValue Base64 | `k3Jx...=` |

- Separador `|`, sin espacios. Opciones de `ConstructorContenidoQr`: relleno de ceros del número y `|` final (por defecto, ninguno).
- Corrección Q; PNG 20 px por módulo con zona de silencio de 4 módulos, en `CPE\QR\yyyy\MM`.
- Tamaño impreso total: **3.5 × 3.5 cm** (A4) y **3.2 × 3.2 cm** (ticket); zona de silencio ≈ 2.4 mm ≥ 1 mm. Prueba unitaria que valide tamaño y zona de silencio en mm.
- Ubicación: parte inferior de la representación impresa.
