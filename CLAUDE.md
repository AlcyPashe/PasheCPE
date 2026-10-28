# PasheCPE — instrucciones para Claude Code

Librería .NET 10 de Pashe Soluciones para comprobantes y guías electrónicas SUNAT: UBL 2.1, validación local (XSD + XSL SUNAT con emulación DataPower + reglas complementarias del Excel, incluidas las de 2027), firma, ZIP, envío SOAP a SUNAT u OSE, GRE por API REST con OAuth2, control total de respuestas, QR y PDF A4. La usarán todos los sistemas de ventas; el primer piloto es Palestra v2 (Bottega, la segunda integración).

## Idioma
- Responde, explica y comenta en español. Nombres de dominio en español; las clases UBL conservan los nombres del estándar (`Invoice`, `InvoiceLine`, `TaxTotal`…).

## Documentos (leer antes de implementar)
- `docs/01-PRD-PasheCPE.md` — qué y por qué (prevalece ante conflicto).
- `docs/02-TRD-PasheCPE.md` — cómo (diseño técnico, tablas de estados, endpoints).
- `docs/03-Plan-Implementacion-PasheCPE.md` — orden de trabajo por fases.
- `docs/04-Guia-Integracion.md` y `docs/05-Mantenimiento-Normativo.md` — consumo y mantenimiento.
- `docs/Contrato-Bottega.md` es la versión anterior del contrato de Bottega; la equivalencia está en el TRD §26. Si encuentras otra diferencia, señálala y pregunta.
- `recursos/` contiene las fuentes oficiales de SUNAT: no las modifiques.

## Forma de trabajo
- Una fase del plan a la vez. Antes de escribir código, presenta el plan de la fase y espera mi aprobación.
- Al cerrar una fase: `dotnet build PasheCPE.slnx` sin advertencias, `dotnet test PasheCPE.slnx` en verde, checklist de la fase y entrada en `CHANGELOG.md`.
- Tareas marcadas **[Desarrollador]** o valores **[verificar]**: detente y pregúntame.

## Principios no negociables
1. No depende de ningún sistema, de EF Core ni de base de datos. Recibe modelos y opciones; devuelve resultados.
2. Sin componentes de terceros para comprobantes electrónicos (GasperSoft, OpenInvoicePeru, Greenter… solo como consulta). Dependencias permitidas: las del TRD §1 (BCL, paquetes Microsoft, `QRCoder`, `QuestPDF` solo en `PasheCPE.Pdf`). Cualquier otra, consúltame.
3. Las respuestas de negocio y de red nunca lanzan excepciones: se devuelven como estados (`SinConexionInternet`, `ServicioNoDisponible`, `ErrorServicio`, `RespuestaIncierta`, `Rechazado`…).
4. Nunca reenviar automáticamente un `sendBill`: ante respuesta incierta o código 1033, primero `getStatusCdr`.
5. Almacenamiento: `CERT` (sin subcarpetas) y `DATA`, `FIRMA`, `ENVIO`, `RPTA`, `REPO`, `QR`, `LOG` con `yyyy\MM` según la fecha de emisión. Sin subcarpetas por día.
6. XML: UTF-8 sin BOM; fechas `yyyy-MM-dd`; montos con `CultureInfo.InvariantCulture`; nunca reindentar un XML firmado.
7. Tasas, plazos, endpoints, catálogos y códigos de error son datos versionados en `Recursos\`, no constantes en el código.
8. Fechas y horas en zona `America/Lima` mediante `TimeProvider`.
9. Las pruebas no llaman a SUNAT ni al OSE. Los envíos reales solo ocurren desde `samples\` y los ejecuto yo manualmente. Las GRE solo existen en producción.
10. No leas ni muestres certificados `.pfx` ni contraseñas; en pruebas usa un certificado autofirmado generado en tiempo de ejecución. Credenciales de la demo en `dotnet user-secrets`.

## Entorno
- Windows, PowerShell, VS Code. Proyecto en `D:\Sistemas\PasheCPE`.
- Git: remoto `origin` = https://github.com/AlcyPashe/PasheCPE. Haz commits al cerrar cada fase; **`git push` solo cuando yo lo pida**. Nunca subas `.pfx`, secretos ni carpetas de salida.

## Comandos
- Compilar: `dotnet build PasheCPE.slnx`
- Pruebas: `dotnet test PasheCPE.slnx`
- Empaquetar: `dotnet pack PasheCPE.slnx -c Release -o artifacts`
- Recursos desde el Excel de SUNAT: `dotnet run --project tools/GeneradorRecursos` · Referencia UBL: `pwsh tools/generar-ubl.ps1`
