# Cambios

Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); versiones con SemVer.

## [Sin publicar]

### Agregado
- 22/10/2026 — Fase 3 (validación SUNAT con XSL y emulación DataPower):
  - `MotorXslSunat`: las 7 XSL oficiales embebidas desde `recursos/XSL` sin cambios, compiladas una sola vez con `XslCompiledTransform`; devuelve todos los errores y observaciones de una pasada y nunca lanza (`PCPE-2101`, `PCPE-2102`). La XSL de la GRE (1.0) se compila pero no se usa.
  - Emulación de DataPower: `validate_utils.xsl`, `error_utils.xsl` y `dat_fechas.xml` propios; `ResolutorRecursosLocales` (`local:///`); extensiones `dp:variable` (nombre del archivo enviado), EXSLT de fechas (hora de Lima) y de expresiones regulares, y `pcpe:` (catálogos a la fecha de recepción, moneda con `monedas.json`, propiedades de los catálogos 05, 07, 22 y 51, ruta del nodo).
  - `AdaptadorXslDataPower`: 3 correcciones en memoria para que compilen Factura, Boleta, NC y ND (`concat` de un argumento, variables redeclaradas, variables de respaldo de `xsl-compatibilidad.json`).
  - Reglas complementarias (`IReglaCpe`) con código y tipo de `reglas-cpe.json` según la vigencia: `SumatoriaIgv` (3291/4290 con la tasa de las líneas, 18 % o 10.5 %), `TasaIgvUnica` (3462), `TotalPrecioVenta` (3279/4310), `CodigoProducto` (3496) y `ContratoColaboracion` (3497–3502).
  - `ajustes-xsl.json`: la 4290 de la XSL de boleta (0.18 fijo) se suprime y la reemplaza `SumatoriaIgv`; clasificación con motivo de las 51 filas de 01/03 posteriores a la XSL.
  - `ValidadorSunat` (XSL → ajustes → reglas), integrado en `GeneradorInvoice` después del XSD: con errores no escribe en `DATA`; las observaciones van a las advertencias. Respeta `Validacion.XslSunat` y `Validacion.Reglas2027`.
  - `IPrecalentador` (XSL, XSD y serializadores en paralelo); `PasheCpe.Crear` lo lanza en segundo plano si `PrecalentarAlIniciar`.
  - Constantes `UnidadMedida` (catálogo N.° 03) desde `tools/GeneradorRecursos/Datos/unidades-medida.json`.
  - Pruebas:
    - compilación y conteos de correcciones;
    - inventario automático de lo que piden las XSL;
    - dorados (facturas, NC y RC) sin hallazgos y 24 casos tributarios generados sin hallazgos;
    - 30 casos inválidos con su código SUNAT;
    - cobertura de `ajustes-xsl.json`;
    - reglas con su tolerancia y vigencia, incluida la boleta MYPE al 10.5 %;
    - extensiones, resolutor, concurrencia (50 validaciones en paralelo) y tiempo en caliente (< 300 ms).
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
- 22/10/2026 — Fase 3: las XSL de SUNAT encontraron 4 defectos del XML de la Fase 2 que SUNAT habría rechazado:
  - con descuento de línea, el precio de referencia es (valor de venta + tributos) ÷ cantidad (3270);
  - el ISC de monto fijo informa su tasa equivalente (2992/3108);
  - las bolsas ICBPER deben ser la cantidad de la línea (3236, nuevo `PCPE-1219`);
  - el `schemeName` de la detracción es «Codigo de detraccion» (4255).
- 22/10/2026 — Fase 3: `ValidadorModelo` acepta en el número y la descripción del contrato de colaboración cualquier carácter y espacios, sin saltos de línea ni tabulaciones, como el Excel (3501/3502); antes exigía solo letras y dígitos.
- 22/10/2026 — Fase 3: documentación actualizada.
  - TRD: §2, §3.2, §3.3 (`ajustes-xsl.json` y `xsl-compatibilidad.json` en lugar de `reglas-2027.json`), §6 (precalentador), §7, §9.3 y §9.4.
  - Plan: estado de la Fase 3; en la Fase 10, revisar si existe una XSL de la GRE 2.0.
  - Guía de mantenimiento: revisar los ajustes con cada XSL nueva.
  - Guía de integración: precalentador.
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
