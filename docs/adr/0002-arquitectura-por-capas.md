# ADR-0002: Arquitectura por capas con dominio independiente

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

El modelo de dominio de la etapa 3 define 33 invariantes y 13 reglas de negocio. El requerimiento RNF-09 exige que todas tengan cobertura de pruebas automatizadas.

Si las reglas de negocio están mezcladas con el acceso a datos, probarlas exige levantar una base de datos. Eso hace las pruebas lentas, frágiles y costosas de escribir, y en la práctica lleva a que no se escriban.

## Decisión

El código se organiza en cuatro proyectos con dependencias dirigidas hacia adentro:

```
Api ──> Application ──> Domain
 │                         ▲
 └────> Infrastructure ────┘
```

`Domain` no referencia ninguna biblioteca externa. `Application` declara interfaces para lo que necesita del exterior; `Infrastructure` las implementa.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Vertical Slice | Agrupa por caso de uso en lugar de por capa, con menos ceremonia. Se descarta porque las reglas de este dominio son transversales: la numeración, los totales y los estados intervienen en casi todos los casos de uso, y quedarían repartidos o duplicados. |
| Tres capas clásicas | Más rápido de arrancar, pero acopla las entidades al mapeador de datos. Las invariantes quedarían dependiendo de una biblioteca externa, contra RNF-09. |
| Arquitectura hexagonal estricta | Conceptualmente equivalente a lo decidido, con más vocabulario y más indirecciones. No aporta a este tamaño de proyecto. |

## Consecuencias

**Positivas**
- Las reglas de negocio se prueban en milisegundos, sin infraestructura.
- La sustitución del proveedor de validación (RNF-02) sale casi gratis: es una interfaz más.
- El modelo de dominio de la etapa 3 se traduce a código sin deformarse.

**Negativas**
- Más proyectos, más archivos y más ceremonia inicial para funcionalidades simples.
- Un CRUD de adquirentes atraviesa cuatro capas para hacer algo trivial.

**Aceptación del costo**
- Se asume conscientemente. El valor está en las reglas complejas, no en los CRUD.
