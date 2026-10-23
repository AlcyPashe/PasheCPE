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

---

## Fase 4 — Firma, ZIP y QR

**Objetivo:** XML firmado válido, ZIP listo y QR generado.

1. `ProveedorCertificados` (caché, aviso de vencimiento) y `FirmadorXml` (TRD §10).
2. `EmpaquetadorZip` (TRD §11).
3. `ConstructorContenidoQr` y `GeneradorQrPng` (TRD §12).
4. Pipeline `GenerarAsync` completo para 01/03: DATA → validación → FIRMA → ENVIO → QR, con historial.

**Aceptación:** firma verificable con certificado autofirmado; firma en `ext:ExtensionContent`; alteración detectada; digest estable; ZIP con una entrada en raíz; QR con los 10 campos y tamaño/zona de silencio correctos; `GenerarAsync` deja los 4 archivos en las carpetas `yyyy\MM` correctas.

---

## Fase 5 — Envío SOAP, conectividad y control de respuestas (Factura y Boleta)

**Objetivo:** primera factura aceptada en beta con los tres niveles de error controlados.

1. `VerificadorConectividad` (TRD §13).
2. `ConstructorSobreSoap`, `ClienteSoap` (`sendBill`, `getStatusCdr`), `LectorFaultSoap`, timeouts separados, bloqueo por documento.
3. `LectorCdr` y `ClasificadorRespuesta` con la tabla del TRD §5.1, `MensajeUsuario`, `FechaLimiteEnvio`.
4. Reconciliación `RespuestaIncierta` / 1033 vía `getStatusCdr`.
5. `EnviarAsync`, `EmitirAsync`, `ConsultarCdrAsync` en `ServicioCpe`; telemetría y enmascarado.
6. `samples\PasheCPE.Demo`: emisión a beta con user-secrets.
7. **[Desarrollador]** RUC y usuario de beta (`MODDATOS`), certificado de prueba en `CPE\CERT`, ejecución manual de la demo.

**Aceptación (sin red):** pruebas con handler falso para aceptado, observado, rechazado, faults 0111/1033/0100, HTTP 503, timeout de conexión y de lectura, sin Internet. **(Manual, beta):** factura gravada y factura exonerada Amazonía aceptadas, boleta individual aceptada, un rechazo forzado y una clave errónea correctamente clasificados; historial JSON con todos los intentos.

---

## Fase 6 — Resumen diario de boletas

**Objetivo:** boletas informadas por resumen con ticket.

1. Clases perfil `SummaryDocuments` (UBL 2.0), `MapeadorSummary` (estados 1/2/3, notas de boleta, lotes de 500, correlativos) con los campos 2027 del Excel: gratuitas desagregadas, IGV de gratuitas, tasa del IGV y razón social del adquiriente.
2. `sendSummary`, `getStatus`, `ConsultorTicket` (TRD §16); `GenerarResumenAsync`, `ConsultarTicketAsync`, `EsperarTicketAsync`.
3. XSL `ValidaExprRegSummary-1.1.0.xsl` integrada.

**Aceptación:** pruebas de lotes, correlativos y ticket 98→0 / 99; **(manual, beta)** resumen con 2+ boletas aceptado tras el ciclo de ticket y consulta del ticket desde otro proceso.

---

## Fase 7 — Notas de crédito y débito (reglas 2027)

**Objetivo:** NC y ND con las reglas de la R.S. 000048-2026.

1. Clases perfil `CreditNote` / `DebitNote`, mapeadores con `DiscrepancyResponse` y `BillingReference` (un único documento).
2. Validación modelo + XSL NC/ND + reglas 2027 (TRD §9.4).
3. Envío por `sendBill` (o en resumen si modifica boleta, según `ModoEnvioBoletas`).

**Aceptación:** pruebas de cada regla 2027 (una referencia, NC motivo 03 sin cambio de importe, ND motivo 13 sin IGV, serie F/B coherente); **(manual, beta)** NC por devolución total y ND por intereses aceptadas sobre una factura aceptada.

---

## Fase 8 — Comunicación de baja

1. Clases perfil `VoidedDocuments`, `MapeadorVoided`, XSL `ValidaExprRegSummaryVoided-1.0.1.xsl`.
2. Validación del plazo (7 días desde el CDR) y de documentos aceptados.
3. `GenerarBajaAsync` + envío por `sendSummary` + ticket.

**Aceptación:** **(manual, beta)** baja de una factura aceptada concluye correctamente; plazo vencido produce error local.

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
2. Validación modelo + XSD + XSL `ValidaExprRegGuiaRemitente-2.0.1.xsl` (09) + reglas C# (31).
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
