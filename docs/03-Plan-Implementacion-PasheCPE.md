# Plan de Implementación — PasheCPE
## Librería de comprobantes y guías electrónicas SUNAT (.NET 10)

| Campo | Valor |
|---|---|
| Versión | 2.0 — 22/10/2026 (reemplaza el plan 1.0 de "Pashe.Cpe") |
| Documentos base | `01-PRD-PasheCPE.md`, `02-TRD-PasheCPE.md` |
| Estrategia | Incremental; cada fase compila, pasa pruebas y deja algo verificable |
| Prioridad de documentos | Factura/Boleta → Resumen diario → NC/ND → Baja → OSE → GRE → PDF |

> **Instrucciones para Claude Code**
> 1. Una fase a la vez. Antes de escribir código, presenta el plan detallado de la fase y espera aprobación.
> 2. El TRD manda en lo técnico; ante conflicto TRD/PRD prevalece el PRD y se reporta la discrepancia.
> 3. Al cerrar cada fase: `dotnet build PasheCPE.slnx` sin advertencias, `dotnet test PasheCPE.slnx` en verde, checklist de la fase marcado y entrada en `CHANGELOG.md`.
> 4. Si una tarea está marcada **[Desarrollador]**, detente y pídela; no la inventes.
> 5. Valores marcados **[verificar]** en el TRD: confírmalos con el desarrollador antes de fijarlos.

---

## Fase 0 — Fundación del repositorio y la solución

**Objetivo:** solución compilando, repositorio enlazado a GitHub y CI en verde.

1. `git init` en `D:\Sistemas\PasheCPE`, rama `main`, remoto `origin = https://github.com/AlcyPashe/PasheCPE`. Primer commit con docs y recursos. `git push` solo cuando el desarrollador lo indique.
2. Revisar los archivos de raíz ya creados (`global.json`, `Directory.Build.props`, `.editorconfig`, `.gitignore`, `.gitattributes`, `nuget.config`) y ajustarlos si el SDK instalado difiere. Mover `tools\github-workflows\ci.yml` y `publicar.yml` a `.github\workflows\`.
3. Crear `PasheCPE.slnx` y los proyectos del TRD §2: `src\PasheCPE`, `src\PasheCPE.Pdf`, `tests\PasheCPE.Tests`, `tests\PasheCPE.Pdf.Tests`, `samples\PasheCPE.Demo`.
4. Crear `Directory.Packages.props` con las dependencias del TRD §1 en su última versión estable.
5. Metadatos de paquete (PackageId, autores "Pashe Soluciones", repositorio, licencia propietaria, README, Source Link).
6. `recursos\VERSION.md` completo (XSD 28/02/2022, XSL 06/09/2022, origen `https://cpe.sunat.gob.pe/guias-y-manuales`).

**Aceptación:** `dotnet build` y `dotnet test` en verde; `dotnet list package` solo muestra paquetes del TRD §1; el workflow `ci.yml` pasa en GitHub.

**Estado:** completada el 22/10/2026; `ci.yml` en verde en GitHub (commit `7cc2602`).

---

## Fase 1 — Núcleo: modelos, resultados, datos versionados y almacén

**Objetivo:** contratos públicos y utilidades base, sin XML ni red.

1. Modelos de entrada (TRD §4, salvo `GuiaRemisionCpe`, que pasa a la Fase 10) y resultados/estados (TRD §5), serializables con `JsonSerializerContext`.
2. Fuente ya entregada: `recursos\VALIDACIONES\ReglasValidacion-CPE-2026-08-26.xlsx` (TRD §3.2). No editar el Excel.
3. `tools\GeneradorRecursos` (consola C#, `DocumentFormat.OpenXml`, valores calculados de celdas) → `catalogos.json`, `catalogos\cat_XX.xml`, `errores-sunat.json`, `reglas-cpe.json` (con vigencias de `Control de Cambios`) y `CodigosSunat.g.cs` (constantes de catálogo generadas, nombres estables en `Datos\nombres-constantes.json`). Datos curados en `tools\GeneradorRecursos\Datos`. Prueba: el generador es idempotente, reporta conteos por hoja y los recursos del repositorio coinciden con el Excel.
4. `tasas.json`, `plazos.json`, `endpoints.json`, con sus lectores y sobrescritura por configuración; completar `tipo`/`transitorio` en `errores-sunat.json` (lista revisada con el desarrollador).
5. Zona horaria Lima + `TimeProvider`; `NumeroALetras` (soles y dólares, céntimos, millones); `Redondeo`; `NombreArchivoCpe`.
6. `AlmacenCpe` (carpetas CERT, DATA, FIRMA, ENVIO, RPTA, REPO, QR, LOG; `yyyy\MM`, sin días; escritura atómica) y `RegistroHistorial` (JSON por documento).
7. `ReglasRegimen` (NRUS, RER, RMT, General; Amazonía por defecto; MYPE restaurantes/hoteles).
8. `OpcionesPasheCpe` con validación al iniciar; `AddPasheCpe` y `PasheCpe.Crear` (mismo `ServiceCollection`; en esta fase solo expone `Historial`).

**Aceptación:** pruebas de número a letras, tasas por fecha (2025, 2026, 2027, 2028), plazos, rutas año/mes, historial concurrente, reglas de régimen, búsqueda de errores y catálogos.

**Estado:** completada el 22/10/2026 (183 pruebas en verde).

---

## Fase 2 — UBL y serialización: Factura y Boleta (Invoice)

**Objetivo:** XML de factura y boleta sin firmar, correcto y validado contra XSD, en `DATA\`.

1. `tools\generar-ubl.ps1` (referencia en `tools\ubl-referencia\`).
2. Clases del perfil SUNAT para `Invoice` y componentes comunes (TRD §3.1) + pregeneración de serializadores.
3. `CalculadoraTributaria` (TRD §7) y `MapeadorInvoice` (01/03): afectaciones, Amazonía, MYPE, ICBPER, gratuitas, descuentos, anticipos, cuotas, detracción, leyendas, código de producto y GTIN, contrato de colaboración.
4. `SerializadorUbl` (TRD §8) y `ValidadorEsquema` (TRD §9.2).
5. `ValidadorModelo` para 01/03 (TRD §9.1).
6. ✅ **[Desarrollador]** Dorados entregados en `tests\Datos\Dorados` (facturas, notas de crédito, resúmenes y GRE aceptados de Fattura v2 y Oxigenio, con sus CDR). No se modifican.

**Aceptación:** todos los XML generados pasan XSD; comparación con dorados; casos de cálculo: gravada 18 %, exonerada Amazonía, MYPE 10.5 % (2026) y 15 % (2027), mixta, gratuita, ICBPER, crédito con 3 cuotas, detracción, precio con impuestos.

**Estado:** completada el 22/10/2026. Las dos facturas aceptadas solo difieren en lo documentado en `tests/PasheCPE.Tests/Dorados/DIFERENCIAS.md` (leyendas 1000 y 2001 agregadas, dos atributos de la guía vigente); no hay dorado gravado (llegará en la Fase 5).

---

## Fase 3 — Validación SUNAT con XSL y emulación DataPower

**Objetivo:** ejecutar las XSL oficiales en .NET y reportar errores con código SUNAT antes de enviar.

1. Prueba de humo (primer día de la fase): compilar las 7 XSL con `XslCompiledTransform` usando un `ResolutorRecursosLocales` y plantillas utilitarias mínimas; reportar al desarrollador cualquier bloqueo.
2. Inventario automático de todas las plantillas llamadas, parámetros, claves `dp:variable`, funciones EXSLT y rutas `local:///` (TRD §9.3).
3. Implementar `validate_utils.xsl` y `error_utils.xsl` propios (emiten nodos de error/observación), extensiones `dp:` (`variable`), EXSLT (`regexp`, `date`) y `dat_fechas.xml`.
4. `MotorXslSunat` con caché de XSL compiladas y `IPrecalentador`.
5. Reglas complementarias 2027 (TRD §9.4) para 01/03.

**Aceptación:** las 7 XSL compilan; los dorados pasan sin errores; al menos 20 casos inválidos producen el código SUNAT esperado (moneda, tipo de documento del cliente, totales, código de producto, serie…); tiempo de validación < 300 ms en caliente.

**Estado:** completada el 22/10/2026.
- Las 7 XSL compilan con 3 correcciones de compatibilidad en memoria (TRD §9.3). Las 2 facturas, las 2 NC y los 2 RC dorados, y los XML generados de 24 casos tributarios, pasan sin hallazgos.
- 30 casos inválidos producen el código del Excel. La validación en caliente tarda pocos milisegundos.
- `ajustes-xsl.json` clasifica las 51 filas de 01/03 posteriores a la XSL; la 4290 de boleta (0.18 fijo) se suprime y la reemplaza una regla C# con la tasa de las líneas.
- La XSL de la GRE solo se compila (es de la GRE 1.0; ver Fase 10).
- La validación con las XSL encontró 4 defectos del XML de la Fase 2 que SUNAT habría rechazado, ya corregidos: precio de referencia con descuento de línea (3270), tasa del ISC de monto fijo (2992/3108), bolsas ICBPER distintas de la cantidad (3236) y `schemeName` de la detracción (4255).

---

## Fase 4 — Firma, ZIP y QR

**Objetivo:** XML firmado válido, ZIP listo y QR generado.

1. `ProveedorCertificados` (caché, aviso de vencimiento) y `FirmadorXml` (TRD §10).
2. `EmpaquetadorZip` (TRD §11).
3. `ConstructorContenidoQr` y `GeneradorQrPng` (TRD §12).
4. Pipeline `GenerarAsync` completo para 01/03: DATA → validación → FIRMA → ENVIO → QR, con historial.

**Aceptación:** firma verificable con certificado autofirmado; firma en `ext:ExtensionContent`; alteración detectada; digest estable; ZIP con una entrada en raíz; QR con los 10 campos y tamaño/zona de silencio correctos; `GenerarAsync` deja los 4 archivos en las carpetas `yyyy\MM` correctas.

**Estado:** completada el 23/10/2026 (483 pruebas en verde).
- Decisiones con el desarrollador: firma RSA-SHA256/SHA-256 por defecto y sin prefijo `ds:` (como los dorados aceptados; `Sha1` por opción), `IServicioCpe` con solo `GenerarAsync` y `ValidarAsync` (crece por fases), y no se regenera un documento ya enviado (`PCPE-3101`).
- El XML firmado pasa el XSD completo (con la firma) y la validación SUNAT; fuera de la firma conserva los bytes de DATA.
- Errores de certificado y firma como `PCPE-3001`–`3007` (`OrigenError.Firma`), sin excepciones.

---

## Fase 5 — Envío SOAP, conectividad y control de respuestas (Factura y Boleta)

**Objetivo:** primera factura aceptada en beta con los tres niveles de error controlados.

1. `VerificadorConectividad` (TRD §13).
2. `ConstructorSobreSoap`, `ClienteSoap` (`sendBill`, `getStatusCdr`), `LectorFaultSoap`, timeouts separados, bloqueo por documento.
3. `LectorCdr` y `ClasificadorRespuesta` con la tabla del TRD §5.1, `MensajeUsuario`, `FechaLimiteEnvio`.
4. Reconciliación `RespuestaIncierta` / 1033 vía `getStatusCdr`. Faults transitorios según TRD §14:
   - `sendBill` sin reintentos internos (`ServicioNoDisponible` con `EsReintentable = true`).
   - `sendSummary` solo reintenta los faults de disponibilidad.
   - Hasta 3 reintentos en `getStatus`, `getStatusCdr` y el token GRE.
   - El siguiente `EnviarAsync` consulta primero `getStatusCdr` si el último intento fue `RespuestaIncierta` o un fault de constancia o ticket; con un fault de disponibilidad reenvía directamente.
   - 0140: `EnProceso` con `RequiereConsultaEstado = true`, sin reenvío.
5. `EnviarAsync`, `EmitirAsync`, `ConsultarCdrAsync` en `ServicioCpe`; telemetría y enmascarado.
6. `samples\PasheCPE.Demo`: emisión a beta con user-secrets.
7. **[Desarrollador]** RUC y usuario de beta (`MODDATOS`), certificado de prueba en `CPE\CERT`, ejecución manual de la demo.

**Aceptación (sin red):** pruebas con handler falso para aceptado, observado, rechazado, faults 0111/1033/0100, HTTP 503, timeout de conexión y de lectura, sin Internet. **(Manual, beta):** factura gravada y factura exonerada Amazonía aceptadas, boleta individual aceptada, un rechazo forzado y una clave errónea correctamente clasificados; historial JSON con todos los intentos.

**Estado:** completada el 23/10/2026 (595 pruebas en verde). Prueba manual en beta del desarrollador (RUC 20493400364, MODDATOS):
- Aceptadas en beta: factura gravada F001-1, factura exonerada Amazonía F001-2 y boleta individual B001-3, con CDR en `RPTA` y el historial con todos los intentos.
- `SOAPAction: "urn:sendBill"` confirmado.
- El reenvío de un documento aceptado se responde desde el historial; la consulta del CDR en beta devuelve `PCPE-4005`.
- La prueba encontró que `Validacion.Modelo` y `Validacion.Esquema` no se respetaban (defecto de la Fase 2); corregido.
- Rechazo forzado F001-11 (cliente con DNI): `Rechazado` 2800, recibido como fault con **HTTP 200**.
- Beta no valida el usuario ni la clave SOL (también con usuario inexistente; confirmado por el desarrollador en proyectos anteriores). La clasificación de credenciales erróneas (0102–0104) queda **pendiente para la primera emisión en producción**; está cubierta por las pruebas automáticas.
- Decisiones con el desarrollador: sin `getStatusCdr` (beta, boletas, series B) se reenvía el mismo ZIP; `SOAPAction` = `urn:{operacion}` como dato en `endpoints.json`; emisor de la demo en `appsettings.json`.
- Los rechazos de `sendBill` llegan como fault 2000–3999 → `Rechazado` (agregado al TRD §5.1).
- Hallazgo: los CDR SOAP dorados verifican la firma de SUNAT; los de la GRE no (digest), se revisa en la Fase 10.

---

## Fase 6 — Resumen diario de boletas

**Objetivo:** boletas informadas por resumen con ticket.

1. Clases perfil `SummaryDocuments` (UBL 2.0), `MapeadorSummary` (estados 1/2/3, notas de boleta, lotes de 500, correlativos) con los campos 2027 del Excel: gratuitas desagregadas, IGV de gratuitas, tasa del IGV y razón social del adquiriente.
2. `sendSummary`, `getStatus`, `ConsultorTicket` (TRD §16); `GenerarResumenAsync`, `ConsultarTicketAsync`, `EsperarTicketAsync`.
3. XSL `ValidaExprRegSummary-1.1.0.xsl` integrada.

**Aceptación:** pruebas de lotes, correlativos y ticket 98→0 / 99; **(manual, beta)** resumen con 2+ boletas aceptado tras el ciclo de ticket y consulta del ticket desde otro proceso.

**Estado:** completada el 23/10/2026 (668 pruebas en verde). Prueba manual en beta del desarrollador (RUC 20493400364, MODDATOS):
- Boletas B001-1 (gravada) y B001-2 (exonerada) generadas sin enviar; `RC-20261023-1` armado desde FIRMA, ticket `1792782486619` y aceptado en la primera consulta (2 s), con el CDR en `RPTA`; ambas boletas quedaron `Aceptado` en su historial.
- `SOAPAction: "urn:sendSummary"` y `"urn:getStatus"` confirmados en beta.
- La consulta del ticket (caso 10) lo tomó del historial; como el resumen ya estaba aceptado, se respondió desde el historial sin llamar a SUNAT. La consulta real desde otro proceso con el resumen en proceso queda cubierta por la prueba automática (beta resuelve el ticket en segundos).
- B001-3, aceptada individualmente en la Fase 5, no se regeneró (`PCPE-3101`), como corresponde.
- Ajustes tras la prueba: mensaje al cajero propio del resumen al recibir el ticket, categoría de log `SoporteEnvio` y sin «(null)» cuando la respuesta no trae código.
- Decisiones con el desarrollador:
  - las líneas se arman con los totales del modelo o desde el XML firmado de FIRMA (`DocumentoResumenCpe`);
  - el resultado del RC se registra en el historial de cada boleta (rechazado → vuelve a `Generado`);
  - fuera de plazo se envía con advertencia (4443) y la anulación fuera de plazo se bloquea (2957).
- XSD UBL 2.0 en un segundo conjunto; la XSL del RC ya estaba integrada desde la Fase 3. Reglas 2027 del RC en C# (2992, 3504, 3102, 4019, 2022, 3051).
- **[verificar]** en el primer uso real: el código al reenviar un RC ya presentado (2223) y, desde enero de 2027, la regla 2048 con IGV 0.00 en boletas exoneradas.

---

## Fase 7 — Notas de crédito y débito (reglas 2027)

**Objetivo:** NC y ND con las reglas de la R.S. 000048-2026.

1. Clases perfil `CreditNote` / `DebitNote`, mapeadores con `DiscrepancyResponse` y `BillingReference` (un único documento).
2. Validación modelo + XSL NC/ND + reglas 2027 (TRD §9.4).
3. Envío por `sendBill` (o en resumen si modifica boleta, según `ModoEnvioBoletas`).

**Aceptación:** pruebas de cada regla 2027 (una referencia, NC motivo 03 sin cambio de importe, ND motivo 13 sin IGV, serie F/B coherente); **(manual, beta)** NC por devolución total y ND por intereses aceptadas sobre una factura aceptada.

**Estado:** completada el 23/10/2026 (779 pruebas en verde). Prueba manual en beta del desarrollador (RUC 20493400364, MODDATOS), todas aceptadas con código 0 y sin observaciones:
- factura al crédito en 2 cuotas F001-21;
- NC por devolución total FC01-22 sobre F001-1 y ND por intereses FD01-23 sobre F001-2;
- NC 03 en cero FC01-24 y NC 03 con los importes de la línea corregida FC01-25;
- NC 13 (ajuste de cuotas) FC01-26 sobre F001-21.

Decisiones con el desarrollador:
- el documento modificado es opcional solo en la ND 13 (2524);
- la NC 13 (ajuste de cuotas) entra en esta fase;
- validación contra el original solo si está en el historial (`FechaEmision` opcional del documento modificado);
- solo se modifican facturas y boletas;
- NC 03: beta aceptó la NC en cero y con importes, y el Excel no tiene regla para ello → **no se validan sus importes** (se quita la advertencia `PCPE-1420`). Como la prueba fue anterior a la vigencia, se repite en enero de 2027 (`05-Mantenimiento-Normativo.md`).

Otros resultados:
- Fila 2369: beta acepta PriceAmount 0 en las NC 03 y 13 (FC01-24, FC01-26); queda como `xsl`, igual que en la factura.
- Las 2 NC doradas coinciden con lo generado salvo las leyendas 1000 y 2001. No hay ND dorada: 16 notas generadas pasan el XSD y las XSL sin hallazgos.
- La XSL de 2022 tiene condiciones viejas en la 3261 (NC) y la 2524 (ND): se suprimen desde el 01/01/2027 (`desde` en `ajustes-xsl.json`) y las reemplazan reglas C#. Hasta esa fecha aplica la XSL.
- Las reglas con vigencia 2027 (ND 13, 3261 para todos los motivos…) no se pueden probar en beta antes del 01/01/2027; las cubren las pruebas automáticas.

---

## Fase 8 — Comunicación de baja

1. Clases perfil `VoidedDocuments`, `MapeadorVoided`, XSL `ValidaExprRegSummaryVoided-1.0.1.xsl`.
2. Validación del plazo (7 días desde la emisión, regla 2957) y de documentos aceptados.
3. `GenerarBajaAsync` + envío por `sendSummary` + ticket.

**Aceptación:** **(manual, beta)** baja de una factura aceptada concluye correctamente; plazo vencido produce error local.

**Estado:** implementada el 27/10/2026 (828 pruebas en verde); pendiente la prueba manual en beta del desarrollador (casos 17 y 18 de la demo). Decisiones con el desarrollador:
- el plazo se cuenta desde la **emisión** del documento (regla 2957 del Excel, que prevalece); antes el TRD decía «desde la recepción del CDR». Las series numéricas no tienen plazo;
- nuevo estado `EstadoCpe.DeBaja` para el documento cuya RA aceptó SUNAT;
- solo facturas y notas de serie F o numérica; las boletas y notas B se anulan con el estado 3 del resumen diario.

Otros resultados:
- `HistorialCpe.Baja` anota la RA que informa el documento desde que se genera; si la RA se rechaza se quita.
- La XSL de 2022 no detecta documentos repetidos en la RA (su clave incluye el `LineID`): lo controla el modelo (`PCPE-1509`, 2348).
- 20 filas RA del Excel posteriores a la XSL clasificadas en `ajustes-xsl.json`.
- **[verificar]** con el primer caso real: el código al reenviar una RA ya presentada (2324) y `getStatusCdr` 0003 → `DeBaja` (producción).

---

## Fase 9 — Canal OSE

**Objetivo:** enviar por OSE con el mismo contrato público.

1. **[Desarrollador]** Endpoints (pruebas y producción) y forma de las credenciales del OSE del sistema de ventas que lo use, sin claves. No se toman de configuraciones de sistemas anteriores; mientras no exista ese sistema, el canal se implementa con endpoints configurables y queda pendiente la prueba real.
2. `CanalEnvio.Ose` con endpoints, credenciales del OSE y capacidades (`SoportaConsultaCdr`, `SoportaBoletaIndividual`).
3. Diferencias de faults/respuestas del OSE en `LectorFaultSoap` y `errores-sunat.json` (sección OSE).

**Aceptación:** pruebas con respuestas grabadas del OSE; **(manual, cuando exista el sistema de ventas con OSE)** una factura aceptada en el ambiente de pruebas del OSE, si existe.

---

## Fase 10 — Guías de remisión (REST + OAuth2)

**Objetivo:** GRE remitente (09) y transportista (31) en producción.

1. Modelo de entrada `GuiaRemisionCpe` (TRD §4) a partir del Excel GRE 25.09.2026, constantes de los catálogos 18, 20, 61 y 62 en `nombres-constantes.json`, clases perfil `DespatchAdvice`, `MapeadorDespatchAdvice` (09 y 31: modalidades 01/02, vehículos, conductores, M1/L, documentos relacionados, bienes).
2. Validación modelo + XSD + reglas C# del Excel GRE (09 y 31). La XSL `ValidaExprRegGuiaRemitente-2.0.1.xsl` es de la GRE 1.0 y da errores falsos con las GRE 2.0 aceptadas (Fase 3): al preparar esta fase, revisar si SUNAT publicó una XSL de la GRE 2.0 junto al Excel GRE y, si existe, evaluarla con los dorados de `09-GRE` antes de decidir.
3. `ProveedorTokenGre`, `ClienteGre` (envío, ticket, CDR, QR del CDR), manejo 401/422/5xx, protección `PermitirEnvioConAmbienteBeta`.
4. `IServicioGre` completo.
5. **[Desarrollador]** Credenciales de API SUNAT del emisor y un traslado real para el primer envío controlado.

**Aceptación:** pruebas con respuestas grabadas (token, ticket 98→0, 99 con y sin CDR, 401, 422); token reutilizado entre envíos; **(manual, producción)** una GRE remitente aceptada con su QR generado.

---

## Fase 11 — PDF A4 (PasheCPE.Pdf)

1. `LectorRepresentacion` (XML firmado + CDR → modelo de impresión).
2. `DisenoComprobante` (01, 03, 07, 08) y `DisenoGuia` (09, 31) con `MarcaEmisor` (TRD §22).
3. `IGeneradorPdf`, `AddPasheCpePdf`, opción `GenerarPdfAutomaticamente`.

**Aceptación:** PDF de factura exonerada, boleta, NC, ND, GRE 09 y 31 generados desde XML dorados; montos `S/ 0,000.00`, fechas `DD/MM/AAAA`, QR 3.5 × 3.5 cm; revisión visual del desarrollador.

---

## Fase 12 — Endurecimiento

1. Concurrencia (50 emisiones paralelas), `ConfigureAwait(false)`, cachés thread-safe.
2. Benchmarks (`benchmarks\`) contra las metas del TRD §20.
3. Auditoría de logs, historial y enmascarado de credenciales.
4. `IHistorialCpe.ListarPendientesAsync` y ejemplo de `BackgroundService` de reenvío en `04-Guia-Integracion.md`.
5. XML-docs públicos completos, README y guías actualizados.
6. Checklist de prohibiciones (TRD §27) y de requisitos RF/RNF del PRD.

**Aceptación:** suite completa en verde; metas de rendimiento cumplidas o desviación documentada; checklist 100 %.

---

## Fase 13 — Publicación NuGet e integración piloto

1. `publicar.yml` con tag `v1.0.0` → GitHub Packages (`PasheCPE`, `PasheCPE.Pdf`).
2. **Piloto en Bottega**: actualizar su TRD a la tabla del TRD §26, consumir solo el paquete, guardar `ResultadoEnvio` en su SQL Server, reenvíos con `BackgroundService`.
3. Correcciones del piloto como `1.0.x`.

**Aceptación:** Bottega emite factura, boleta por resumen, NC y GRE usando exclusivamente el paquete; integración ≤ 1 día; rechazos y caídas registrados con código y descripción.

---

## Dependencias del desarrollador (resumen)

| Antes de | Qué |
|---|---|
| Fase 1 | ✅ Excel de reglas de validación 26.08.2026 en `recursos\VALIDACIONES` (incluye catálogos y códigos de retorno). Opcional: UNSPSC completo `CCNU_MOD_2.xlsm` en `recursos\CATALOGOS` |
| Fase 2 | XML aceptados por SUNAT para dorados |
| Fase 5 | RUC y usuario beta, certificado de prueba |
| Fase 9 | Endpoints y forma de las credenciales del OSE del sistema de ventas que lo use (sin claves) — pendiente |
| Fase 10 | ✅ Excel de reglas de validación GRE `ReglasValidacion-GRE-2026-09-25.xlsx` en `recursos\VALIDACIONES`. Pendientes: credenciales API SUNAT y un traslado real |
| Fase 13 | PAT de GitHub con `read:packages` en el servidor del cliente |
