# ADR-0010: Orden de adquisición de bloqueos

**Estado:** Aceptada
**Fecha:** 2026-09-28

## Contexto

Hasta H3 solo existía un bloqueo: el del rango de numeración (ADR-0009). Emitir una nota crédito introduce un segundo.

RN-04 exige que la suma de las notas crédito de una factura no supere su total. Comprobarlo requiere sumar las notas existentes y guardar la nueva sin que nadie se cuele en medio, y esa suma es una regla aritmética sobre varias filas: ninguna restricción de PostgreSQL puede expresarla. A diferencia de RN-01, aquí no hay un índice único que actúe como red de seguridad.

Con dos bloqueos aparece un riesgo que con uno no existía. Si un caso de uso tomara la factura y luego el rango, y otro tomara el rango y luego la factura, dos transacciones simultáneas podrían quedarse esperándose mutuamente. PostgreSQL detecta el interbloqueo y aborta una de las dos, pero el integrador recibe un error por una petición correcta.

## Decisión

Todos los casos de uso adquieren los bloqueos en el mismo orden:

1. Documento referenciado.
2. Rango de numeración.

La emisión de factura solo toma el segundo, así que no puede romper la regla. Cualquier caso de uso nuevo que necesite bloquear varias filas debe encajar en esta secuencia o ampliarla por el final, nunca intercalarse.

La regla queda escrita en la documentación de `IRepositorioDocumentos.TomarParaActualizarAsync`, en `EmitirNotaHandler` y en `CLAUDE.md`.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Un solo bloqueo, sobre la factura, y numeración optimista | Rompe la garantía de RN-01, que ADR-0009 resolvió con el rango. No se cambia una regla legal para simplificar otra. |
| Detectar el interbloqueo y reintentar | Convierte un problema evitable por diseño en uno que se gestiona en ejecución. Además, un reintento sobre una transacción que ya consumió un consecutivo exige cuidado adicional. |
| Nivel de aislamiento serializable | Trasladaría el problema: el motor abortaría transacciones por conflictos de serialización y habría que reintentar igual, con menos control sobre cuándo ocurre. |

## Consecuencias

**Positivas**
- El interbloqueo se vuelve imposible por construcción, no improbable por suerte.
- La regla es corta y se puede verificar leyendo cada caso de uso.

**Negativas**
- Es una restricción global que no obliga el compilador. Si alguien añade un caso de uso que tome los bloqueos al revés, nada falla hasta que haya concurrencia real. Por eso está escrita en tres sitios, incluido `CLAUDE.md`.
- La transacción de emisión de notas es más larga que la de facturas: mantiene la factura bloqueada mientras además espera el rango.

## Verificación

`NotasConcurrentesTests` cubre las dos caras:

- `Dos_notas_simultaneas_no_pueden_acreditar_mas_que_la_factura`: dos notas por el valor completo llegan a la vez y solo una es aceptada.
- `Cuatro_notas_simultaneas_que_caben_justo_pasan_todas`: el bloqueo no rechaza de más.

Verificación por mutación: retirando el `FOR UPDATE` de `TomarParaActualizarAsync`, la primera prueba falla con `Expected: 1, Actual: 2`.

Ese modo de fallo es distinto del de ADR-0009 y conviene registrarlo. Allí, sin el bloqueo, el índice único rechazaba el duplicado y el sistema fallaba ruidosamente: nada incorrecto llegaba a persistirse. Aquí no hay red debajo. Sin el bloqueo no se lanza ninguna excepción, no se registra ningún error y ambas notas se guardan: la factura queda acreditada por el doble de su valor y el sistema no da ninguna señal. **El bloqueo no mejora la experiencia de error, es lo único que impide un dato contablemente falso.**
