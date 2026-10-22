# PRD — Documento de Requisitos del Producto
## PasheCPE — Librería de comprobantes y guías electrónicas SUNAT

| Campo | Valor |
|---|---|
| Producto | PasheCPE (paquetes NuGet `PasheCPE` y `PasheCPE.Pdf`) |
| Propietario | Pashe Soluciones (Iquitos, Loreto, Perú) |
| Versión del documento | 2.0 (reemplaza a la 1.0 de julio 2026, "Pashe.Cpe") |
| Fecha | 22/10/2026 |
| Estado | Aprobado para implementación |
| Repositorio | `D:\Sistemas\PasheCPE\` — https://github.com/AlcyPashe/PasheCPE |
| Documentos relacionados | `02-TRD-PasheCPE.md`, `03-Plan-Implementacion-PasheCPE.md`, `04-Guia-Integracion.md`, `05-Mantenimiento-Normativo.md` |

---

## 1. Visión

PasheCPE es la librería propia de Pashe Soluciones, en C# sobre **.NET 10 LTS**, que usan todos los sistemas de ventas ASP.NET Core (actuales y futuros) para **generar, validar, firmar, empaquetar, enviar y controlar** los comprobantes de pago electrónicos (CPE) y las guías de remisión electrónicas (GRE) ante SUNAT o un OSE. Con ella, ningún sistema vuelve a implementar la facturación electrónica; los cambios normativos se resuelven publicando una nueva versión del paquete.

Reemplaza al componente comercial GasperSoft y a la clase monolítica `CPE.cs` del sistema Fattura.

## 2. Problema

- Cada sistema reimplementa la facturación electrónica de forma distinta.
- GasperSoft omite datos que SUNAT exige y obliga a parchar los XML.
- `CPE.cs` (≈2 800 líneas) mezcla UBL, firma, ZIP, envío, CDR y QR con configuración estática; no soporta varias empresas, ni OSE, ni GRE por API REST, y no distingue fallas de red de rechazos.
- No hay historial de envíos ni trazabilidad independiente de la base de datos del sistema de ventas.

## 3. Objetivos y métricas

| Objetivo | Métrica de éxito |
|---|---|
| Estandarizar la emisión electrónica en un solo paquete | 0 líneas de lógica UBL/firma/envío en los sistemas de ventas |
| Integración rápida | Un sistema nuevo emite su primera factura aceptada en beta en ≤ 1 día |
| Control total de respuestas | 100 % de los envíos termina en un estado tipado (nunca una excepción no controlada), con código y descripción |
| Trazabilidad | Por cada documento quedan en disco XML, XML firmado, ZIP, CDR, PDF, QR e historial JSON de intentos |
| Adaptabilidad normativa | Un cambio de SUNAT se resuelve con una nueva versión del paquete, sin tocar los sistemas de ventas |
| Eficiencia | Generación + firma + ZIP de una factura < 150 ms tras el precalentamiento (sin contar red) |

## 4. Consumidores

Los "usuarios" son **sistemas**, no personas:

- Sistemas de ventas ASP.NET Core de Pashe Soluciones (Bottega, Salute, Vestiti, Bevande, etc.), instalados en servidores locales de clientes de Loreto (exonerados de IGV por la Ley de la Amazonía) y, a futuro, de otras regiones (gravados).
- Servicios en segundo plano (Worker Service / `BackgroundService`) de esos sistemas, para reenvíos y consulta de tickets.
- Aplicaciones de consola o herramientas internas (por ejemplo, migración del eFacturador de Comercializadora San Juan, cliente PRICO que envía por OSE eFact).

Fuera de los consumidores soportados: proyectos .NET Framework 4.x (no pueden referenciar `net10.0`).

## 5. Alcance

### 5.1 Documentos (lista cerrada)

| Documento | Código | UBL | Canal SUNAT | Canal OSE |
|---|---|---|---|---|
| Factura | 01 | 2.1 | SOAP `sendBill` | SOAP `sendBill` |
| Boleta de venta | 03 | 2.1 | Resumen diario (por defecto) o `sendBill` individual | `sendBill` o resumen |
| Nota de crédito | 07 | 2.1 | `sendBill` (o en resumen si modifica boleta) | ídem |
| Nota de débito | 08 | 2.1 | `sendBill` (o en resumen si modifica boleta) | ídem |
| Resumen diario de boletas | RC | 2.0 | SOAP `sendSummary` + ticket | ídem |
| Comunicación de baja | RA | 2.0 | SOAP `sendSummary` + ticket | ídem |
| Guía de remisión remitente | 09 | 2.1 | API REST + OAuth2 + ticket | — (siempre SUNAT) |
| Guía de remisión transportista | 31 | 2.1 | API REST + OAuth2 + ticket | — (siempre SUNAT) |

### 5.2 Fuera de alcance

- Comprobantes de retención (20) y percepción (40), resumen de reversiones (RR), liquidación de compra (04), DAE/DAEE (documentos de atribución de la R.S. 000048-2026).
- Envío de GRE por OSE.
- Persistencia en base de datos: la librería **no guarda nada en BD**; recibe datos y devuelve resultados.
- Interfaz de usuario y formato PDF de ticket (80 mm). Solo PDF A4.
- Consulta de RUC o del régimen tributario en servicios de SUNAT: el sistema de ventas informa el régimen.
- Envío por correo electrónico de los comprobantes.

## 6. Requisitos funcionales

### Generación

**RF-01 — UBL 2.1 desde XSD oficiales.** Las clases de los documentos se derivan de los XSD de SUNAT ubicados en `recursos\XSD` (XSD 2.1 actualizado al 28/02/2022; Summary y Voided en UBL 2.0). Cubre los 8 documentos del alcance, con los elementos que exigen las guías de elaboración de SUNAT.

**RF-02 — Serialización correcta.** XML UTF-8 sin BOM, prefijos `cac`, `cbc`, `ext`, `ds`, `sac` exactos, orden del XSD, montos con `InvariantCulture`, sin elementos vacíos, nombres oficiales `RUC-TT-SERIE-NUMERO`, `RUC-RC-yyyyMMdd-n`, `RUC-RA-yyyyMMdd-n`.

**RF-03 — Cálculo tributario en la librería.** El sistema de ventas entrega cantidades, precios y afectaciones; la librería calcula bases, impuestos, totales, redondeos y leyendas (incluido el monto en letras).

**RF-04 — Régimen y exoneración por emisor.** El emisor informa su régimen (`NRUS`, `RER` (Especial), `RMT` (MYPE Tributario), `General`) y dos indicadores que sí cambian el XML:
- `ExoneradoAmazonia` — **por defecto `true`** (opción global `ExoneradoAmazoniaPorDefecto`, sobrescribible por emisor). Afectación por defecto 20, tributo 9997 EXO, leyenda Amazonía (Catálogo 52: 2001 bienes / 2002 servicios / 2003 construcción).
- `TasaEspecialRestaurantesHoteles` — MYPE de restaurantes, hoteles y alojamientos: tasa según el año de emisión (2026: 10.5 %; 2027: 15 %).
Reglas por régimen: el `NRUS` solo emite boletas y sus notas (y GRE); `RER`, `RMT` y `General` no alteran el XML. Cada línea puede sobrescribir su afectación (operaciones mixtas, gratuitas, inafectas, exportación).

**RF-05 — Tasas con vigencia.** IGV, IPM, tasa especial MYPE e ICBPER se resuelven por **fecha de emisión** desde una tabla de vigencias embebida y sobrescribible por configuración; nunca valores fijos en el código.

**RF-06 — Reglas comerciales.** Forma de pago Contado/Crédito con cuotas, detracción (Catálogo 54), ICBPER, anticipos, descuentos globales y por línea, operaciones gratuitas, tipo de operación (Catálogo 51), código de producto SUNAT (8 dígitos, UNSPSC) y GTIN por línea, documentos relacionados, orden de compra, contrato de colaboración empresarial (campo opcional 2027).

### Validación local (nivel 1 de error)

**RF-07 — Validación en tres capas, antes de enviar:**
1. **Modelo** (C#): datos obligatorios, coherencia de serie/tipo, documento de identidad del cliente (factura → RUC; boleta > S/ 700 → identificación), fechas y plazos, NRUS solo boletas.
2. **Esquema**: el XML generado contra los XSD oficiales.
3. **Reglas SUNAT**: las XSL oficiales de `recursos\XSL` (actualizadas al 06/09/2022), ejecutadas con **emulación de las extensiones IBM DataPower** (`dp:`) y EXSLT, más un **conjunto complementario de reglas 2027** (R.S. 000048-2026) implementado en C#.
El resultado indica código SUNAT, descripción y ubicación de cada error; un documento con errores no se envía.

**RF-08 — Reglas vigentes al 01/01/2027.** Se implementan solo las reglas de la R.S. 000048-2026 (postergada por la R.S. 000143-2026): una NC/ND modifica un único comprobante; NC motivo 03 corrige descripción o código de producto sin cambiar importes; ND motivo 13 (penalidades, inafectas); ND motivo 03 "Otros conceptos"; series F/B coherentes con el documento modificado; código de producto de 8 dígitos; catálogos 09 y 10 actualizados.

### Firma, empaquetado y QR

**RF-09 — Firma digital.** XML-DSig enveloped (RSA-SHA1/SHA1, perfil configurable a SHA-256 si SUNAT lo cambia) dentro de `ext:UBLExtensions`, con el certificado `.pfx` del emisor ubicado en `CPE\CERT\`. Expone el **valor resumen** (DigestValue).

**RF-10 — ZIP.** Un ZIP por documento con el XML firmado en la raíz.

**RF-11 — QR.**
- Comprobantes (01, 03, 07, 08): la librería **construye** el contenido oficial `RUC|TIPO|SERIE|NUMERO|IGV|TOTAL|FECHA|TIPODOCADQ|NUMDOCADQ|VALORRESUMEN` y genera el PNG.
- Guías (09, 31): SUNAT entrega en el CDR el enlace del QR; la librería lo extrae y genera el PNG.

**RF-12 — PDF A4 (paquete `PasheCPE.Pdf`).** Dos diseños fijos, alimentados por el **XML firmado** más los datos de marca del emisor (logo, teléfonos, correo, web):
- **Diseño Comprobante**: factura, boleta, nota de crédito y nota de débito (estas últimas con el bloque de documento modificado y motivo).
- **Diseño Guía**: guía remitente y guía transportista.
Formato de montos `S/ 0,000.00` y fechas `DD/MM/AAAA`. Incluye valor resumen, QR, leyendas y texto de representación impresa.

### Envío y control de respuestas

**RF-13 — Canales.** SUNAT (beta y producción) u **OSE** (eFact y otros, endpoints configurables) por emisor, mediante SOAP 1.1 construido a mano con WS-Security. GRE siempre por la API REST de SUNAT con OAuth2 y **solo en producción** (SUNAT no tiene ambiente de pruebas GRE).

**RF-14 — Verificación de conectividad.** Antes de cada envío, la librería comprueba (con resultado cacheado unos segundos) si hay red, si hay Internet y si el servicio destino responde, y devuelve `SinConexionInternet` o `ServicioNoDisponible` sin intentar el envío. También ofrece `VerificarConectividadAsync()` para que el sistema de ventas la consulte.

**RF-15 — Clasificación completa de respuestas.** Toda operación devuelve un resultado tipado con uno de estos estados:

| Nivel | Estado | Origen |
|---|---|---|
| 0 | `SinConexionInternet` | Sin red o sin Internet |
| 0 | `ServicioNoDisponible` | Servicio caído, HTTP 5xx, timeout de conexión, TLS, o fault de SUNAT tipo "el sistema no puede responder su solicitud" |
| 1 | `ErrorValidacionLocal` | Validación previa (RF-07) |
| 2 | `ErrorServicio` | Fault SOAP / error HTTP no transitorio (credenciales, perfil, estructura, archivo), p. ej. `soap-env:Client.0111` |
| 2 | `RespuestaIncierta` | Se envió pero no hubo respuesta (timeout de lectura): se debe reconciliar antes de reenviar |
| 3 | `Aceptado` | CDR `ResponseCode = 0` sin notas |
| 3 | `AceptadoConObservaciones` | CDR `0` con notas 4000+ |
| 3 | `Rechazado` | CDR 2000–3999 |
| 3 | `EnProceso` | Ticket recibido / estado 98 |

Cada resultado incluye: código, descripción literal de SUNAT/OSE, descripción del catálogo oficial de errores, observaciones, ticket, valor resumen, contenido QR, rutas de archivos, HTTP status, fault, tiempos, endpoint, intento, `EsReintentable`, `RequiereConsultaEstado`, `FechaLimiteEnvio` y un `MensajeUsuario` en español claro para el cajero.

**RF-16 — Reconciliación sin duplicados.** Ante `RespuestaIncierta` o el código 1033 ("registrado previamente"), la librería consulta el CDR (`getStatusCdr`) antes de cualquier reenvío. Nunca reenvía automáticamente un `sendBill` sin reconciliar.

**RF-17 — Tickets.** Resumen, baja y GRE: envío → ticket → consulta con espera progresiva configurable (`98` en proceso, `0` procesado, `99` con errores) → CDR. El ticket se devuelve y queda en el historial para consultarlo en otro proceso o día.

**RF-18 — Resumen diario automático.** Agrupa boletas y notas por fecha, divide en lotes de máximo 500 líneas y asigna correlativos.

**RF-19 — Historial de envíos sin BD.** Cada documento tiene un `CPE\LOG\yyyy\MM\{nombre}.json` con todos sus intentos (fecha y hora de Lima, canal, endpoint, operación, duración, estado, códigos, ticket, HTTP). La librería lo devuelve en el resultado y permite consultarlo y listar pendientes del periodo.

### Almacenamiento

**RF-20 — Directorio CPE.** El sistema de ventas indica la ruta base. La librería crea y administra:

```
{RutaBase}\                      ej. D:\CPE
├── CERT\                        certificados .pfx (sin subcarpetas)
├── DATA\yyyy\MM\                XML sin firmar
├── FIRMA\yyyy\MM\               XML firmados
├── ENVIO\yyyy\MM\               ZIP para envío
├── RPTA\yyyy\MM\                CDR (R-*.zip)
├── REPO\yyyy\MM\                PDF
├── QR\yyyy\MM\                  PNG del QR
└── LOG\yyyy\MM\                 historial JSON por documento
```
Solo hasta el mes: **sin subcarpetas por día**. El periodo es el de la **fecha de emisión** (resumen y baja: fecha de generación; el CDR se guarda en el periodo de su documento).

### Integración

**RF-21 — Multiempresa sin estado.** Los datos del emisor (RUC, razón social, domicilio, régimen, canal, credenciales, certificado, marca) viven en el sistema de ventas y se pasan en cada llamada.

**RF-22 — Uso con y sin inyección de dependencias.** `services.AddPasheCpe(configuration.GetSection("PasheCPE"))` y `services.AddPasheCpePdf()` para ASP.NET Core / Worker; fábrica `PasheCpe.Crear(opciones)` para consola u otros usos.

**RF-23 — Operaciones por etapa y de punta a punta.** `EmitirAsync` (todo el flujo) y operaciones separadas `GenerarAsync`, `EnviarAsync`, `ConsultarCdrAsync`, `ConsultarTicketAsync`, `GenerarPdfAsync`, para que el sistema de ventas pueda generar sin Internet y enviar después.

**RF-24 — Fecha y hora de Lima.** Toda fecha generada por la librería (emisión por defecto, historial, plazos) usa la zona `America/Lima`, independientemente de la zona del servidor.

## 7. Requisitos no funcionales

| ID | Requisito |
|---|---|
| RNF-01 | `net10.0`, C# 14, `nullable enable`, desarrollo en VS Code + .NET CLI + Claude Code. |
| RNF-02 | **Sin librerías de terceros para comprobantes electrónicos** (GasperSoft, OpenInvoicePeru, Greenter, etc. solo como consulta). Utilitarias permitidas: BCL, paquetes Microsoft, `QRCoder` (QR) y `QuestPDF` (PDF, solo en `PasheCPE.Pdf`, licencia Community). Cualquier otra se consulta. |
| RNF-03 | Asíncrono de punta a punta, `CancellationToken` en toda operación, seguro para uso concurrente (servicios singleton sin estado mutable compartido). |
| RNF-04 | Eficiencia: certificados, XSL compiladas, esquemas y serializadores se cachean; precalentamiento opcional al iniciar. |
| RNF-05 | Las respuestas de negocio y de red **nunca lanzan excepciones**; solo errores de programación o configuración inválida (validada al iniciar). |
| RNF-06 | Observabilidad: `ILogger`, `ActivitySource` y `Meter` "PasheCPE" (OpenTelemetry compatible). Credenciales siempre enmascaradas. |
| RNF-07 | Seguridad: nunca escribir claves en logs ni en el historial; los `.pfx` no se versionan; TLS 1.2+. |
| RNF-08 | Distribución: NuGet privado en **GitHub Packages** (`AlcyPashe`), SemVer, CI en GitHub Actions. |
| RNF-09 | Mantenibilidad: cada regla, catálogo o endpoint en un solo lugar; catálogos, tasas, plazos y errores como datos versionados. |
| RNF-10 | Pruebas automáticas sin red; envíos reales solo desde `samples\` ejecutados manualmente por el desarrollador. |
| RNF-11 | Resultados serializables con `System.Text.Json` para que el sistema de ventas los guarde tal cual. |

## 8. Supuestos y dependencias

- Servidores Windows locales con acceso a Internet (a veces intermitente en Loreto).
- Cada emisor tiene certificado digital vigente, usuario secundario SOL (o credenciales OSE) y credenciales de API SUNAT para GRE.
- Pruebas de CPE en el ambiente beta de SUNAT; las GRE se prueban en producción con traslados reales (un rechazo en producción no genera documento válido).
- El desarrollador provee en `recursos\`: XSD, XSL, el Excel oficial de reglas de validación (actualizado al 26.08.2026; incluye catálogos y códigos de retorno), las reglas de validación GRE y, para OSE.

## 9. Riesgos

| Riesgo | Mitigación |
|---|---|
| XSL de 2022 no contienen las reglas 2027 | Capa complementaria de reglas 2027 en C#, versionada y probada |
| XSL dependen de `validate_utils.xsl`, `error_utils.xsl` y catálogos XML no publicados por SUNAT | Implementación propia de esas plantillas y catálogos generados desde el Anexo 8 |
| `XslCompiledTransform` no soporta elementos de extensión DataPower | Las plantillas utilitarias propias no los usan; `dp:variable` y EXSLT se emulan como funciones de extensión |
| No hay XSL oficial para GRE transportista | XSD + reglas C#; revisar publicaciones de SUNAT |
| GRE sin ambiente de pruebas | Validación local completa + primer envío controlado en producción |
| Cambios de endpoints (SUNAT/OSE) | Endpoints 100 % configurables |
| Cambios de tasas (IPM 2027–2029, MYPE 2027) | Tabla de vigencias como dato |
| Caída de SUNAT o de Internet | Estados tipados, fecha límite, generación sin conexión y reenvío posterior |
| Envíos duplicados por timeout | Reconciliación obligatoria (RF-16) y bloqueo por documento |

## 10. Decisiones pendientes (no bloquean el inicio)

1. Afectación por defecto de las boletas de un emisor `NRUS` (confirmar con contador): por ahora usa la misma regla que el resto (exonerado si Amazonía).
2. Endpoint beta del OSE eFact y soporte de `getStatusCdr` en eFact: tomar de la configuración del eFacturador.
3. Si SUNAT publica XSL actualizadas para 2027, reemplazar las de 2022 y retirar las reglas C# duplicadas.
