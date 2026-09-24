# ADR-0005: Emisión asíncrona

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

La validación ante la DIAN tiene una latencia típica de 2 a 8 segundos y puede fallar.

Un diseño donde la API mantiene abierta la conexión del integrador durante esa espera produce tres problemas:

1. Conexiones bloqueadas acumulándose a la espera de un tercero sin control.
2. Ante un corte de red, el documento queda en estado indeterminado: no se sabe si la autoridad lo recibió. Reintentar puede duplicarlo; no reintentar puede dejar aprobado en la DIAN algo que el sistema cree fallido.
3. El integrador recibe un error de tiempo de espera con el que no puede hacer nada útil.

El criterio de éxito CE-04 exige que ningún documento quede en estado indeterminado.

## Decisión

La recepción del documento y su validación ante la autoridad son operaciones separadas.

La API recibe el documento, asigna consecutivo, verifica invariantes, persiste y responde de inmediato con un identificador y el estado `RECIBIDO`. Nunca contacta al servicio externo durante la petición.

Un proceso en segundo plano genera, firma, transmite y consulta el resultado. El integrador conoce el veredicto consultando el estado del documento.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Emisión síncrona | Más fácil de construir y de explicar, pero no puede satisfacer CE-04. El estado indeterminado es inherente al diseño. |
| Asíncrona con espera opcional | El integrador podría pedir esperar hasta cierto tiempo por el resultado. Es más cómodo de integrar, pero duplica los caminos de ejecución y, por tanto, las pruebas. Queda como candidata a versión 2. |

## Consecuencias

**Positivas**
- Ningún documento queda en estado indeterminado. Cada uno tiene un estado explícito en todo momento.
- La API responde rápido y de forma predecible, sin depender de un tercero (RNF-03).
- El sistema sigue aceptando emisiones aunque el servicio de validación esté caído (RNF-04).

**Negativas**
- El integrador debe hacer dos pasos: emitir y luego consultar.
- Hay que construir y operar un proceso en segundo plano.
- Se necesita un mecanismo que garantice que ningún documento quede sin procesar. Eso lo resuelve ADR-0006.
