# ADR-0009: Bloqueo pesimista en la asignación de consecutivos

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

RN-01 establece que un número de documento se asigna una sola vez. RNF-06 exige que esto se cumpla bajo emisión concurrente. La consecutividad de la numeración es una exigencia legal, no una comodidad técnica.

Calcular el siguiente número consultando el máximo emitido y sumando uno falla en cuanto dos peticiones coinciden: ambas leen el mismo valor y generan el mismo número.

## Decisión

Al asignar un consecutivo, la fila del rango de numeración se bloquea hasta el final de la transacción. Una segunda petición sobre el mismo rango espera, lee el valor ya actualizado y continúa.

El bloqueo recae sobre una sola fila y dura lo que toma incrementar un contador. La etapa 3 definió `RangoNumeracion` como agregado propio precisamente para que así fuera.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Secuencia de la base de datos | No respeta límites ni vigencia: desconoce que el rango tiene número final y fecha de vencimiento. Además no se revierte si la transacción falla, lo que dejaría huecos en la numeración. |
| Concurrencia optimista con reintento | Evita el bloqueo, pero bajo contención produce reintentos frecuentes. Para un contador de acceso puntual, el bloqueo es más simple y más predecible. |
| Restricción de unicidad y manejo del error | La base de datos impediría el duplicado, pero el integrador recibiría un error por algo que el sistema debía resolver. Se mantiene la restricción como red de seguridad, no como mecanismo principal. |

## Consecuencias

**Positivas**
- La garantía es del motor de base de datos, no de código que deba acordarse de algo.
- La numeración queda sin huecos ni repeticiones, como exige la norma.
- El bloqueo es de alcance mínimo, lo que limita su impacto en el rendimiento.

**Negativas**
- Las emisiones sobre un mismo rango se serializan. Es una limitación de rendimiento inherente a la consecutividad exigida.
- Requiere que la transacción sea corta: nada lento puede ocurrir mientras el bloqueo está tomado.

**Restricción que se deriva**
- Ninguna llamada a un servicio externo puede ocurrir dentro de la transacción que toma el consecutivo. ADR-0005 ya lo garantiza al sacar la validación externa del camino de la petición.
