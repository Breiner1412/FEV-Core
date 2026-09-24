# ADR-0008: Autenticación por llave de API

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

RF-01 exige rechazar toda petición sin credencial válida y RF-02 exige saber qué integrador originó cada documento.

El consumidor de esta API es otro sistema, no una persona. No hay pantalla de inicio de sesión, no hay sesión que expire y no hay usuario que renueve nada.

## Decisión

Cada sistema integrador recibe una llave de API que envía en cada petición. El sistema almacena únicamente una huella criptográfica de la llave, nunca la llave. El valor en texto plano se muestra una sola vez, al crearse.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| JWT con usuarios y roles | Es el estándar de la industria y se vería más completo en un portafolio, pero los tokens con expiración están diseñados para sesiones de usuario. Aplicarlos aquí obligaría a construir un flujo de renovación que ningún actor de este sistema necesita. Sería complejidad sin propósito. |
| OAuth 2.0 con credenciales de cliente | Es la respuesta correcta cuando hay múltiples aplicaciones y ámbitos de permiso diferenciados. Este sistema tiene un tipo de consumidor con un solo conjunto de permisos. |
| Sin autenticación en la versión 1 | Ahorraría tiempo, pero deja sin cumplir RF-01 y RF-02, y se leería como un descuido en un sistema que maneja documentos fiscales. |

## Consecuencias

**Positivas**
- Menos código y menos superficie de error que un esquema de tokens.
- La trazabilidad de RF-02 es directa: la llave identifica al integrador en cada petición.
- Revocar el acceso de un integrador es desactivar su llave.

**Negativas**
- Una llave filtrada es válida hasta que alguien la revoque, mientras que un token expira solo.
- No hay permisos diferenciados: un integrador autenticado puede hacer todo.

**Compensación**
- La llave viaja siempre sobre HTTPS y nunca se registra en los archivos de actividad.
- Los permisos diferenciados quedan documentados como candidatos a versión 2, cuando exista un caso de uso que los justifique.
