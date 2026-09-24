# ADR-0003: PostgreSQL como motor de base de datos

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

El sistema necesita persistencia transaccional. Dos requerimientos imponen exigencias concretas:

- **RNF-06**: la asignación de consecutivos debe ser correcta bajo concurrencia, lo que exige bloqueo a nivel de fila.
- **RNF-12**: los valores monetarios deben manejarse con precisión decimal exacta.

La bandeja de salida (ADR-0006) exige además que la tarea pendiente y el documento se escriban en una misma transacción.

## Decisión

Se usa **PostgreSQL** como único motor de base de datos, en todos los entornos incluidas las pruebas de integración.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| SQL Server | Igualmente capaz y con integración natural en .NET. Se descarta por licenciamiento y porque PostgreSQL aparecía explícitamente en las ofertas que motivaron el proyecto. |
| MySQL | Conocido por el autor. Su manejo de bloqueos y tipos decimales es correcto, pero PostgreSQL tiene mejor comportamiento en concurrencia y un ecosistema más sólido en .NET. |
| SQLite en pruebas, PostgreSQL en producción | Haría las pruebas más rápidas, pero SQLite no reproduce el comportamiento de bloqueo de filas. Las pruebas de RNF-06 pasarían sin probar nada real. |

## Consecuencias

**Positivas**
- Bloqueo pesimista de fila disponible, que es lo que ADR-0009 necesita.
- Tipo decimal de precisión arbitraria, apto para valores monetarios.
- El mismo motor en desarrollo, pruebas y producción elimina una clase entera de sorpresas.

**Negativas**
- Las pruebas de integración requieren un contenedor en ejecución, y son más lentas que con una base en memoria.

**Mitigación**
- Solo las pruebas de integración usan base de datos real. Las de dominio y aplicación no tocan persistencia.
