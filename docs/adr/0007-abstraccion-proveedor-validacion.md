# ADR-0007: Abstracción del proveedor de validación y simulador independiente

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

Conectarse al servicio real de la DIAN exige un proceso de habilitación con registro formal, certificado digital emitido por entidad autorizada y aprobación de un set de pruebas. Es un trámite administrativo cuya duración no depende del desarrollo.

La sección 7.1 del documento de visión decidió operar contra un simulador, e impuso que el acceso al servicio quede detrás de una abstracción. RNF-02 recoge esa exigencia.

## Decisión

Se define la interfaz `IProveedorValidacion` en la capa `Application`, con las operaciones de transmitir un documento y consultar el resultado de una transmisión.

El simulador se construye como un **servicio independiente** (`FevCore.DianSimulator`) que se despliega en su propio contenedor y se comunica por HTTP real. Permite configurar demoras artificiales, respuestas de rechazo y caídas, para ejercitar los caminos de fallo.

Cambiar al servicio real consiste en registrar otra implementación de la interfaz y apuntar una variable de entorno.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Implementación falsa dentro del mismo proceso | Más simple, pero nunca ejercita red, tiempos de espera ni errores de conexión, que es exactamente donde están los casos difíciles que ADR-0005 y ADR-0006 pretenden resolver. |
| Intentar la habilitación real desde el inicio | Bloquearía el desarrollo por un trámite, no por una dificultad técnica. Queda como fase posterior. |
| Doble implementación, en memoria y como servicio | Lo más completo, pero son dos implementaciones que mantener sincronizadas. |

## Consecuencias

**Positivas**
- El proyecto avanza sin depender de trámites externos.
- La abstracción se ejercita de verdad: al hablar por HTTP, los fallos de red son reales y las pruebas de RNF-04 y RNF-05 prueban algo.
- El simulador permite provocar a voluntad escenarios que con el servicio real serían difíciles de reproducir.

**Negativas**
- Un servicio más que construir y mantener.
- El XML generado no se valida contra el servicio real. Se materializa el riesgo R-02 del documento de visión.

**Mitigación de R-02**
- El XML se valida contra el esquema oficial del estándar y contra ejemplos públicos.
- La limitación se declara abiertamente en el README del proyecto.
