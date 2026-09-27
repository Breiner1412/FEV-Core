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

## Verificación

La prueba `NumeracionConcurrenteTests.Emitir_en_paralelo_no_repite_ni_salta_consecutivos`
emite 25 facturas simultáneas contra PostgreSQL real y comprueba que los consecutivos
entregados no se repiten y forman un tramo continuo.

Una prueba de concurrencia puede pasar sin probar nada: basta con que las peticiones
se separen lo suficiente para no llegar a competir. Para descartarlo se retiró el
`FOR UPDATE` de `RepositorioRangos` y se volvió a ejecutar: la prueba falló con
`500`, que es la restricción de unicidad rechazando el consecutivo duplicado. Con el
bloqueo restaurado, vuelve a pasar.

Ese resultado precisa además el reparto de responsabilidades entre los dos mecanismos:

- La **restricción de unicidad** sobre `(prefijo, consecutivo)` protege la integridad.
  Sin el bloqueo, la base de datos nunca habría guardado dos facturas con el mismo
  número; habría rechazado la segunda.
- El **bloqueo** protege la disponibilidad. Sin él, 24 de cada 25 integradores reciben
  un error interno por una petición correcta.

El bloqueo no se añade porque la restricción sea insuficiente para los datos, sino
porque delegar en ella convierte una carrera previsible en un error para quien
consume la API.

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
