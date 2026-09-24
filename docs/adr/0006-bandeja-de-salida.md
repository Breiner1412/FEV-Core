# ADR-0006: Bandeja de salida para el trabajo pendiente

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

ADR-0005 establece que un proceso en segundo plano procesa los documentos después de que la API responde. Falta definir cómo se entera ese proceso de que hay trabajo.

La solución intuitiva es guardar el documento y luego encolar la tarea. Eso deja una ventana: si el proceso se detiene entre ambas operaciones, el documento queda persistido y nadie lo procesa. No se produce ningún error visible; el documento simplemente permanece en `RECIBIDO` de forma indefinida.

Es el problema de **escritura doble**: dejar consistentes dos sistemas sin una transacción que cubra a ambos.

## Decisión

La tarea pendiente se persiste como una fila en la misma base de datos, dentro de la **misma transacción** que persiste el documento.

Un servicio en segundo plano, alojado en el mismo proceso de la API, consulta periódicamente esa tabla y procesa lo que encuentre, con toma exclusiva de tareas, espera creciente entre reintentos, límite de intentos y recuperación de tareas abandonadas.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Cola de mensajes dedicada (RabbitMQ) | Es lo correcto a escala, pero no elimina el problema de escritura doble por sí sola: seguiría existiendo entre la base de datos y la cola. Agrega un servicio más que operar sin que este proyecto lo necesite. |
| Hangfire | Resuelve programación y reintentos con menos código, y trae panel web. Se descarta porque oculta dentro de una biblioteca justo el mecanismo que el proyecto busca demostrar, y porque su almacenamiento de trabajos no comparte transacción con el dominio. |
| Ejecutar la tarea en segundo plano sin persistirla | Lo más simple, y pierde todo el trabajo pendiente ante cualquier reinicio. |

## Consecuencias

**Positivas**
- Ningún documento se pierde, aunque el proceso muera en el peor instante posible.
- No se agrega infraestructura: la base de datos ya estaba.
- El mecanismo queda visible en el código del proyecto, que es un objetivo explícito.

**Negativas**
- El trabajador consulta periódicamente en lugar de recibir un aviso, lo que introduce una latencia igual al intervalo de consulta.
- Escalar a varios trabajadores exige que la toma de tareas sea correcta bajo concurrencia.

**Compensación**
- La latencia del intervalo es despreciable frente a los segundos que tarda la validación externa.
