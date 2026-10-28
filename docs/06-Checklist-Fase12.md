# Checklist de endurecimiento (Fase 12)

Revisión del 27/10/2026 sobre `main` después de la Fase 11. Para cada punto se indica la evidencia: la prueba automática, la búsqueda o la medición que lo demuestra.

- Pruebas: **983 en verde** (`PasheCPE.Tests` 890, `PasheCPE.Pdf.Tests` 62, `GeneradorRecursos.Tests` 31).
- `dotnet build PasheCPE.slnx`: sin advertencias, con las advertencias tratadas como errores.

Leyenda:
- ✅ cumple;
- ⏳ cumple en las pruebas, pero falta la prueba real con un cliente;
- ⚠️ parcial (se indica qué falta).

## 1. Prohibiciones (TRD §27)

| # | Prohibición | Estado | Evidencia |
|---|---|---|---|
| 1 | Ninguna dependencia fuera del TRD §1 | ✅ | `dotnet list package`: PasheCPE usa solo paquetes Microsoft y `QRCoder`; PasheCPE.Pdf, solo `QuestPDF`. `BenchmarkDotNet` está solo en `benchmarks\` y no se distribuye (aprobado el 27/10/2026, agregado al TRD §1). |
| 2 | Ninguna tasa, plazo, endpoint o código de error en el código | ✅ con dos observaciones | Las tasas, plazos, endpoints, catálogos y errores están en `Recursos\*.json`, y `TablaTasasTests` y `TablaPlazosTests` prueban que se sobrescriben por configuración. Observaciones: (a) `ReglasSummary2027` copia literalmente los valores que admiten las reglas RC 118-124 y 158-164 del Excel («10.5 y 18», «10, 10.5 y 18»); (b) el `scope` OAuth2 de la GRE (`https://api-cpe.sunat.gob.pe`) es una constante del protocolo, no un endpoint al que se llama. Ver la §4. |
| 3 | Ningún reenvío automático de `sendBill` | ✅ | `EnvioTests`: `EnviarAsync_TrasRespuestaInciertaEnProduccion_ConsultaYNoReenvia`, `…Fault1033EnProduccion_ConsultaElCdrYDevuelveElEstadoReal` y `…FaultDeDisponibilidad_ReintentableSinReintentoInterno…`. `ConcurrenciaTests.EnviarAsync_50VecesElMismoDocumento_UnSoloSendBill`. |
| 4 | Ninguna excepción por respuesta de negocio o de red | ✅ | Los `throw` de `src` son solo de argumentos, de configuración o de recursos embebidos faltantes (errores de programación). Las fallas de red se prueban en `EnvioTests` (timeout, conexión cortada, HTTP 503), `EnvioGreTests` (401, 422, 5xx) y `VerificadorConectividadTests`. |
| 5 | Ninguna credencial en logs, historial o repositorio; ningún `.pfx` versionado | ✅ | `AuditoriaCredencialesTests` (nueva): con todos los niveles de log, revisa SUNAT, OSE y GRE, con respuesta incierta, ticket y 401, en los logs y en cada archivo del almacén salvo `CERT`. `git ls-files '*.pfx' '*.p12' '*.pem' '*.key'` no devuelve nada, y el `.gitignore` los excluye. |
| 6 | Ninguna reindentación de XML firmado | ✅ | `FirmadorXmlTests.Firmar_SinBomYSinReindentar_ConservaLosBytesFueraDeLaFirma`; `FirmadorXml.Guardar` escribe con `Indent = false` y `NewLineHandling.None`. |
| 7 | Ningún acceso a base de datos | ✅ | Ninguna coincidencia de `SqlClient`, `EntityFramework`, `DbContext`, `Dapper` ni `SqlConnection` en `src`. |
| 8 | Ninguna subcarpeta por día en el almacén | ✅ | `AlmacenCpeTests.Ruta_UsaCarpetaAnioYMesSinDia`. `AlmacenCpe` es el único que compone rutas. |
| 9 | Ninguna llamada real a SUNAT ni al OSE desde las pruebas | ✅ | `ServiciosPrueba` reemplaza el `HttpMessageHandler` por `SunatFalso`, que falla la prueba si no hay una respuesta programada, y la red por `RedFalsa`. Las pruebas con `PasheCpe.Crear` solo generan, sin enviar. |

## 2. Requisitos funcionales (PRD)

| RF | Requisito | Estado | Evidencia |
|---|---|---|---|
| RF-01 | UBL 2.1 desde los XSD oficiales | ⚠️ | 01, 03, 07, 08, RC, RA y 09 pasan el XSD y coinciden con los dorados (`DoradosInvoiceTests`, `DoradosCreditNoteTests`, `MapeoGreTests`). Falta la 31: Fase 10b, postergada hasta tener un cliente transportista. |
| RF-02 | Serialización correcta | ✅ | `GenerarAsync_Xml_EsUtf8SinBomYSinXsi`, dorados normalizados y `ModelosTests.ReferenciaCpe_NombresDeArchivo`. |
| RF-03 | Cálculo tributario en la librería | ✅ | `CalculadoraTributariaTests`, `CasosTributarios`, `NumeroALetrasTests` y `RedondeoTests`. |
| RF-04 | Régimen y exoneración por emisor | ✅ | `ReglasRegimenTests` y `AfectacionPorDefecto_*`. |
| RF-05 | Tasas con vigencia | ✅ | `TablaTasasTests` (`Igv_ConTramosConfigurados_ReemplazaLosEmbebidos`, `MypeRestaurantesHoteles_SegunFechaDeEmision`). |
| RF-06 | Reglas comerciales | ✅ | `CasosTributarios`: contado y crédito con cuotas, detracción, ICBPER, anticipos, descuentos y gratuitas. |
| RF-07 | Validación en tres capas | ✅ | `ValidadorModeloTests`, `ValidadorEsquemaTests`, `MotorXslSunatTests` y `ReglasComplementariasTests`. |
| RF-08 | Reglas vigentes al 01/01/2027 | ✅ | `ReglasComplementariasTests` y `ServicioNotasTests` (motivos 03 y 13, una sola referencia, series F/B). |
| RF-09 | Firma digital | ✅ | `FirmadorXmlTests`: SHA-256 y SHA-1, `CheckSignature` y alteración detectada. |
| RF-10 | ZIP | ✅ | `EmpaquetadorZipTests`. |
| RF-11 | QR | ✅ | `QrTests` y `QrPdfTests.BloqueQr_SeImprimeDe35x35Mm`. |
| RF-12 | PDF A4 | ⚠️ | 01, 03, 07, 08 y 09, aprobados por el desarrollador. Falta el diseño de la 31 (Fase 10b). |
| RF-13 | Canales | ⏳ | SUNAT beta probado con envíos reales (demo). OSE eFact y GRE: solo con respuestas grabadas, hasta el primer cliente con OSE y el primer traslado real. |
| RF-14 | Verificación de conectividad | ✅ | `VerificadorConectividadTests`. Se expone como `IVerificadorConectividad.VerificarAsync`. |
| RF-15 | Clasificación completa de respuestas | ✅ | `ClasificadorRespuestaTests` y `ModelosTests.Nivel_SegunEstado`. |
| RF-16 | Reconciliación sin duplicados | ✅ | Igual que la prohibición 3. |
| RF-17 | Tickets | ✅ | `EnvioResumenTests` (98→0 y 99) y `EnvioGreTests`. Consulta desde otro proceso con el ticket del historial. |
| RF-18 | Resumen diario automático | ✅ | `ResumenDiarioTests` (lotes de 500, correlativos) y `ConcurrenciaTests` (20 RC y 20 RA en paralelo). |
| RF-19 | Historial sin BD | ✅ | `RegistroHistorialTests` (50 escrituras simultáneas) y `IHistorialCpe.ListarPendientesAsync`. El ejemplo de `BackgroundService` está en la Guía §5. |
| RF-20 | Directorio CPE | ✅ | `AlmacenCpeTests`. |
| RF-21 | Multiempresa sin estado | ✅ | El emisor viaja en cada llamada; los servicios son singleton sin estado mutable compartido (§3). |
| RF-22 | Con y sin inyección de dependencias | ✅ | `Crear_SinDi_UsaLosMismosServiciosQueAddPasheCpe` y `AddPasheCpePdf_DesdeLaConfiguracion_…`. |
| RF-23 | Operaciones por etapa y de punta a punta | ✅ | `EmitirAsync`, `GenerarAsync`, `EnviarAsync`, `ConsultarCdrAsync`, `ConsultarTicketAsync` y `IGeneradorPdf.GenerarAsync`, que equivale a `GenerarPdfAsync`. |
| RF-24 | Fecha y hora de Lima | ✅ | `RelojCpe_ConvierteAHoraDeLima` y `Fechas_Date_UsaLaFechaDeLima`. |

## 3. Requisitos no funcionales (PRD)

| RNF | Requisito | Estado | Evidencia |
|---|---|---|---|
| RNF-01 | `net10.0`, C# 14, `nullable` | ✅ | `Directory.Build.props`. |
| RNF-02 | Sin librerías de terceros para CPE | ✅ | Prohibición 1. |
| RNF-03 | Asíncrono, `CancellationToken` y concurrencia | ✅ | La regla CA2007 (`ConfigureAwait(false)`) es un error de compilación. `ConcurrenciaTests` (nueva): 50 facturas en paralelo con sus archivos e historiales, 50 envíos del mismo documento → 1 `sendBill`, 20 RC y 20 RA sin correlativos repetidos, 20 GRE con un solo token. `GeneracionAutomaticaTests.EmitirAsync_20EnParalelo_CadaUnaConSuPdf`. Ver la revisión de cachés más abajo. |
| RNF-04 | Cachés y precalentamiento | ✅ | `Obtener_DosVeces_ReutilizaLaCache` (certificado) y `Precalentador_CompilaTodoSinErrores`. Benchmarks en la §5. |
| RNF-05 | Sin excepciones por negocio o red | ✅ | Prohibición 4. |
| RNF-06 | Observabilidad | ✅ | `TelemetriaTests` (nueva): actividades `generar`, `validar` y `firmar` anidadas, estado de error y la métrica `pashecpe.validacion.errores`. Ya existían las actividades y métricas de envío. |
| RNF-07 | Seguridad | ✅ | Prohibición 5. TLS lo negocia el sistema operativo: la librería no fija protocolos. |
| RNF-08 | Distribución | ⏳ | `ci.yml` y `publicar.yml` existen. La primera publicación (`v1.0.0`) es la Fase 13. El resultado del CI en GitHub no se revisó desde aquí. |
| RNF-09 | Mantenibilidad | ✅ | Datos versionados en `Recursos\` y `ReglasExcelSunat` como única fuente de las reglas. Ver las observaciones de la prohibición 2. |
| RNF-10 | Pruebas sin red | ✅ | Prohibición 9. |
| RNF-11 | Resultados serializables | ✅ | `SerializacionJsonTests` (nueva): ida y vuelta de cada modelo y resultado de `PasheCpeJson`, incluida la `GuiaRemisionCpe` que faltaba en el contexto. Nuevo `PasheCpePdfJson` para `ResultadoPdf`. |

### Revisión de cachés y estado compartido

Todos los servicios son singleton. Sus cachés y estado:

| Componente | Caché o estado | Mecanismo |
|---|---|---|
| `ProveedorCertificados` | Certificado por ruta | `ConcurrentDictionary`; recarga bajo `Lock` con doble verificación |
| `MotorXslSunat` | XSL compiladas | `ConcurrentDictionary<string, Lazy<…>>` (`ExecutionAndPublication`) |
| `SerializadorUbl` | `XmlSerializer` por tipo | `ConcurrentDictionary` |
| `ValidadorEsquema` | Esquemas | `Lazy<XmlSchemaSet>` |
| `ExtensionesXsl` | Expresiones regulares | `ConcurrentDictionary` |
| Catálogos | Datos | `Lazy<FrozenDictionary>` |
| `VerificadorConectividad` | Resultado por host | `ConcurrentDictionary` |
| `ProveedorTokenGre` | Token por credencial | `ConcurrentDictionary` y un `SemaphoreSlim` por credencial |
| `BloqueoDocumentos` | Bloqueo por documento | `Dictionary` bajo `Lock` |
| `RegistroHistorial` | Escrituras | `SemaphoreSlim` por documento (hash) |

Los únicos campos mutables sin sincronizar pertenecen a objetos de una sola llamada (`RecolectorResultadosXsl`, el lector de `ValidadorEsquema`) o a las clases UBL. Los diccionarios `static readonly` de las reglas solo se leen.

## 4. Observaciones para el desarrollador

1. **[verificar] Tasa MYPE de 2027 y la regla 3504 del resumen diario.** Desde el 01/01/2027, `tasas.json` aplica el 15 % a restaurantes y hoteles MYPE. En cambio, las filas RC 123-124 y 163-164 del Excel de SUNAT (26/08/2026, error 3504) solo admiten 10.5 y 18 en boletas, y 10, 10.5 y 18 en notas. Con ese Excel, una boleta MYPE con 15 % en un resumen de 2027 sería rechazada por la validación local y, si SUNAT aplica su regla, también en SUNAT. Lo mismo pasa con la observación 4019 (filas 118-119 y 158-159). Hay que revisarlo cuando SUNAT publique el próximo Excel. Si se confirma el 15 %, conviene mover los valores admitidos a un recurso de datos.
2. El `scope` de la GRE quedó como constante en `ClienteGre`. Si se prefiere, puede pasar a `endpoints.json` junto a `GreToken`.

## 5. Rendimiento (TRD §20)

BenchmarkDotNet 0.15.8, en el mismo proceso (`InProcessEmitToolchain`). Equipo: Intel Core i7-8700 (6 núcleos), Windows 11, .NET 10.0.13 en Release. Factura de 20 líneas por la API pública (`PasheCpe.Crear`), con escritura en disco: DATA, FIRMA, ENVIO, QR y LOG.

| Medición | Media | Desv. | Memoria | Meta | Resultado |
|---|---:|---:|---:|---:|---|
| Generar + firmar + ZIP (+ QR e historial), sin XSL | 18.3 ms | 0.39 ms | 5.0 MB | < 150 ms | ✅ 8 veces por debajo |
| Validación de nivel 1 con XSL (modelo, XSD, XSL y reglas) | 4.1 ms | 0.12 ms | 2.5 MB | < 300 ms | ✅ |
| Generar completo con las opciones por defecto | 23.5 ms | 0.68 ms | 7.0 MB | — | referencia |
| PDF A4 | 21.9 ms | 0.40 ms | 0.7 MB | — | referencia |

Son tiempos en caliente: la primera emisión compila las XSL, los XSD y los serializadores (≈ 1 s), y eso lo cubre el precalentamiento. Para repetir la medición: `dotnet run -c Release --project benchmarks/PasheCPE.Benchmarks -- --filter "*"`.

## 6. Pendientes que no son de la Fase 12

- Fase 10b: guía transportista (31) y su PDF.
- Con el primer cliente:
  - envío real por OSE eFact;
  - primera GRE real;
  - los **[verificar]** del JSON de la GRE.
- Demo: una emisión en beta con el PDF automático (casos 1 a 3).
- Fase 13: publicación `v1.0.0` y piloto en Bottega.
