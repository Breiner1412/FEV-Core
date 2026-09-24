# ADR-0001: Plataforma .NET y ASP.NET Core

**Estado:** Aceptada
**Fecha:** 2026-09-24

## Contexto

El proyecto necesita una plataforma para construir una API REST con procesamiento en segundo plano, firma digital de XML y acceso a base de datos relacional.

Existe una restricción adicional que no es técnica: uno de los objetivos declarados del proyecto es demostrar competencia en el ecosistema .NET, que aparece con frecuencia en ofertas de integración tributaria.

El autor tiene experiencia previa en PHP con Laravel y en TypeScript, y ninguna en .NET.

## Decisión

Se usa **.NET 10 (LTS)** con **ASP.NET Core** para la API y **C#** como lenguaje.

Se elige la versión con soporte de largo plazo en lugar de la más reciente, porque recibe correcciones por más tiempo y es la que un equipo elegiría para un sistema que debe operar durante años.

## Alternativas consideradas

| Alternativa | Por qué se descartó |
|---|---|
| Laravel con PHP | Es donde el autor es más productivo hoy, pero no cumple el objetivo de aprendizaje ni corresponde al perfil de las ofertas que motivaron el proyecto. |
| Node.js con NestJS | Reutilizaría el conocimiento de TypeScript, pero la firma digital de XML tiene soporte considerablemente más maduro en .NET. |
| Java con Spring Boot | Plataforma igualmente válida y madura. Se descarta porque no aporta sobre .NET y el objetivo de aprendizaje apunta a .NET. |

## Consecuencias

**Positivas**
- La firma digital de XML está cubierta por la biblioteca estándar, sin dependencias de terceros.
- C# fue diseñado por el mismo autor que TypeScript, lo que acorta la curva de aprendizaje desde la experiencia previa.
- El tipado fuerte y los tipos de referencia no anulables ayudan a hacer cumplir invariantes en tiempo de compilación.

**Negativas**
- El autor es nuevo en la plataforma. Se materializa el riesgo R-03 del documento de visión.
- El desarrollo inicial será más lento que en una plataforma conocida.

**Mitigación**
- Un hito dedicado exclusivamente a aprendizaje, con un entregable mínimo, antes de implementar reglas de negocio.
