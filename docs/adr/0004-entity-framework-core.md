# ADR-0004: Entity Framework Core como mapeador

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

Se necesita traducir entre las entidades del dominio y las tablas de PostgreSQL. ADR-0002 exige que el dominio no dependa de la herramienta que haga esa traducción.

## Decisión

Se usa **Entity Framework Core**, configurado por fuera de las entidades mediante clases de configuración que viven en `Infrastructure`.

Las entidades del dominio no llevan atributos de mapeo, no heredan de clases base del mapeador y no exponen propiedades que existan solo para satisfacerlo.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Dapper | Más control sobre el SQL y más rápido, pero exige escribir a mano el mapeo de agregados con colecciones anidadas, que es justo la forma de este modelo. |
| Ambos combinados | EF Core para escritura, Dapper para consultas de solo lectura. Es una combinación válida y común, pero agrega una segunda herramienta antes de tener evidencia de que la primera no alcanza. |

## Consecuencias

**Positivas**
- Las migraciones quedan versionadas en el repositorio y se aplican automáticamente al arrancar.
- El mapeo de agregados con colecciones anidadas viene resuelto.
- El seguimiento de cambios reduce el código de persistencia.

**Negativas**
- Es fácil escribir consultas que generan SQL ineficiente sin darse cuenta.
- La configuración por fuera de las entidades es más verbosa que anotarlas directamente.

**Compromiso asumido**
- Si una consulta resulta problemática, se resuelve con SQL explícito en `Infrastructure` antes que introduciendo otra herramienta.
