# Cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); versiones con SemVer.

## [Sin publicar]

### Agregado
- 22/10/2026 — Fase 2 (UBL y serialización de factura y boleta):
  - `tools/generar-ubl.ps1` y `.config/dotnet-tools.json` (`dotnet-xscgen` 3.0.1430): referencia UBL completa en `tools/ubl-referencia/` (no se versiona).
  - Perfil UBL 2.1 de SUNAT (`PasheCPE.Ubl`: `Invoice`, `InvoiceLine` y agregados comunes), públicas pero ocultas y fuera del contrato estable; serializadores pregenerados (`PasheCPE.XmlSerializers.dll`, incluido en el paquete).
  - `SerializadorUbl`: UTF-8 sin BOM, sin sangría, prefijos `cac`/`cbc`/`ext`, `InvariantCulture`, sin elementos vacíos; ida y vuelta sin cambios.
  - `CalculadoraTributaria` (manda el precio con impuestos; gravado, exonerado, inafecto, exportación, gratuitas, ICBPER, ISC 01/02, descuentos y cargos 02/03/49/50, anticipos 04/05/06, detracción, leyendas desde el catálogo 52) y `MapeadorInvoice`.
  - `ValidadorEsquema` contra los XSD 2.1 incrustados (sin resolver nada externo) y `ValidadorModelo` para 01/03 (RUC con dígito verificador, series, plazos, catálogos vigentes, cliente, líneas, pagos, detracción, contrato de colaboración, cargos y anticipos).
  - `GeneradorInvoice`: modelo → cálculo → UBL → XSD → `DATA\yyyy\MM`.
  - `parametros.json` (boleta sin identificar hasta S/ 700, contrato de colaboración desde el 01/01/2027); `AnticipoCpe.Afectacion`; historial del ICBPER desde 2019 en `tasas.json`.
  - Pruebas contra las facturas aceptadas de `tests/Datos/Dorados/01-Factura` (comparación por estructura y valores, diferencias documentadas) y casos límite de redondeo contra las tolerancias de SUNAT.
- 22/10/2026 — Fase 1 (núcleo):
  - `tools/GeneradorRecursos`: Excel de reglas CPE 26.08.2026 → `catalogos.json` (45 catálogos), `catalogos/cat_XX.xml`, `errores-sunat.json` (2 077 códigos, 20 transitorios), `reglas-cpe.json` (2 623 reglas con vigencia y control de cambios) y `CodigosSunat.g.cs` (15 clases de constantes con nombres estables). Datos curados en `tools/GeneradorRecursos/Datos`. Salida determinista; `GeneradorRecursos.Tests` comprueba la idempotencia y que los recursos estén al día.
  - Modelos de entrada (`EmisorCpe`, `ComprobanteCpe`, `NotaCpe`, `ResumenDiarioCpe`, `ComunicacionBajaCpe`, `ReferenciaCpe`…) y resultados (`EstadoCpe`, `ResultadoEnvio`, `ResultadoGeneracion`, `HistorialCpe`…) con el contexto `PasheCpeJson`. Las credenciales no se serializan y su `ToString()` las enmascara.
  - Datos versionados `tasas.json`, `plazos.json`, `endpoints.json` (`billConsultService` con `verificado: false`, sin OSE), `monedas.json` y `errores-locales.json`, con sus lectores y sobrescritura por configuración.
  - `CatalogosSunat` (código existente y vigente a la fecha, con el número de catálogo en el error), `CatalogoErrores`, `TablaTasas`, `TablaPlazos`, `CatalogoEndpoints` (advertencia única por endpoint no verificado).
  - Hora de Lima (`RelojCpe` sobre `TimeProvider`), `NumeroALetras`, `Redondeo`, `NombreArchivoCpe`.
  - `AlmacenCpe` (`yyyy\MM`, sin días, escritura atómica, sin rutas fuera del almacén), `RegistroHistorial` e `IHistorialCpe` (`ListarPendientesAsync`).
  - `ReglasRegimen` (NRUS, Amazonía por defecto, tasa MYPE con respaldo a la general).
  - `OpcionesPasheCpe` con validación al iniciar, `AddPasheCpe` y `PasheCpe.Crear` (mismo `ServiceCollection`).
- 22/10/2026 — Fase 0: repositorio git (`main`, remoto `origin`), solución `PasheCPE.slnx` con `src/PasheCPE`, `src/PasheCPE.Pdf`, `tests/PasheCPE.Tests`, `tests/PasheCPE.Pdf.Tests` y `samples/PasheCPE.Demo`; `Directory.Packages.props` (CPM) con las dependencias del TRD §1; `src/` y `tests/Directory.Build.props`; metadatos NuGet (licencia propietaria `LICENSE.txt`, README, Source Link del SDK, PDB embebido); pruebas de humo de los dos ensamblados; `tests/Datos/`.

### Cambiado
- 22/10/2026 — `UseAppHost=false` en `tools/GeneradorRecursos` y `samples/PasheCPE.Demo`: se ejecutan con `dotnet.exe` (firmado por Microsoft) en lugar de un `.exe` propio, que el antivirus bloqueaba. No aplica a los proyectos de prueba: xUnit v3 exige el `.exe` (`UseAppHost=true`); queda anotado en `tests/Directory.Build.props`.
- 22/10/2026 — Fase 2: constantes del catálogo 53 con nombres curados (`DescuentoGlobalAfectaBase`, `AnticipoGravado`…). TRD §1, §2, §3.3, §7, §8, §9.2, §12 (nodo del QR de la GRE confirmado con CDR reales) y §15 (entrada `dummy/` del CDR); README (contrato estable).
- 22/10/2026 — Pruebas: los RUC de ejemplo de la Fase 1 no tenían dígito verificador válido; se corrigieron (`20123456786`, `20987654326`).
- 22/10/2026 — Fase 1: nueva dependencia `Microsoft.Extensions.DependencyInjection` (Microsoft, ya transitiva) para `PasheCpe.Crear`; `.gitattributes` fija LF en los recursos generados. TRD §1, §2, §3.2, §4 y §23, Plan (Fases 0, 1 y 10) y guía de mantenimiento actualizados; `GuiaRemisionCpe` pasa a la Fase 10.
- 22/10/2026 — `global.json` fijado en el SDK 10.0.401 con `latestPatch`. Workflows movidos de `tools/github-workflows/` a `.github/workflows/` y actualizados a `actions/checkout@v7` y `actions/setup-dotnet@v6`.
- 22/10/2026 — Pruebas con `xunit.v3.mtp-off` + `xunit.runner.visualstudio` (VSTest): con el SDK 10, `xunit.v3` 4.x usa Microsoft Testing Platform v2 y no corre bajo VSTest.
- 22/10/2026 — `.editorconfig`: CA1707 desactivada en `tests/` (nombres `Metodo_Escenario_Resultado`).

### Documentación
- 22/10/2026 — Registrado `recursos/VALIDACIONES/ReglasValidacion-GRE-2026-09-25.xlsx` en `VERSION.md`; PRD, TRD y Plan actualizados (Excel GRE entregado; endpoints OSE pendientes hasta contar con un sistema de ventas que use OSE, sin tomar configuraciones de sistemas anteriores; paquetes de prueba del TRD §1).
- 22/10/2026 — PRD, TRD y Plan 2.0: nombre `PasheCPE`, reglas SUNAT 2027 (R.S. 000048-2026 y 000143-2026), canal OSE, GRE solo producción, validación XSL con emulación DataPower, conectividad, historial `LOG`, paquete `PasheCPE.Pdf` (A4), NuGet en GitHub Packages.
