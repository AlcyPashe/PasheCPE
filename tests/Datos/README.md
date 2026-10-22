# Datos de prueba

Archivos que usan las pruebas automáticas, sin acceso a red:

- `Dorados\` — XML aceptados por SUNAT, usados como referencia de la serialización (Fase 2).
- CDR reales y respuestas SOAP/REST grabadas (SUNAT, OSE y GRE) para las pruebas de transporte y clasificación.

`.gitattributes` marca `tests/Datos/**` como `-text`: los archivos se conservan byte a byte para no alterar XML firmados. No guardar aquí certificados `.pfx`, claves ni credenciales.
