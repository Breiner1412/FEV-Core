# FEV-Core

API de facturación electrónica para Colombia (DIAN). Proyecto de portafolio:
importa tanto el proceso como el código.

## Lo primero que hay que leer

- `docs/06-plan-entregas.md` — qué entra en cada hito y qué no. **Es el contrato
  de alcance.** No adelantes trabajo de un hito posterior.
- `docs/02-requerimientos.md` — RF, RN, RNF y la máquina de estados (sección 6).
- `docs/03-modelo-dominio.md` — entidades, agregados e invariantes (`INV-*`).
- `docs/adr/` — decisiones tomadas y por qué. No las contradigas sin abrir una ADR nueva.

Cada pieza de código debe poder rastrearse a un requisito. Si no aparece en los
documentos, no se implementa: se pregunta.

## Arquitectura

Capas, con dependencias en una sola dirección (ADR-0002):

```
Domain  ←  Application  ←  Infrastructure
                        ←  Api
```

- `FevCore.Domain` — no referencia nada. Ni EF, ni ASP.NET, ni nada de fuera.
- `FevCore.Application` — casos de uso e interfaces de repositorio (`Abstracciones/`).
- `FevCore.Infrastructure` — EF Core, PostgreSQL, implementaciones de repositorio.
- `FevCore.Api` — controladores, contratos HTTP, autenticación.

**Dónde va cada regla:** una invariante que un objeto puede defender solo va en el
dominio; una que necesita ver otros objetos va en la aplicación, y la parte
aritmética se le pasa al dominio como parámetro. Ejemplos vivos:
`Documento.EmitirNota` recibe la factura entera para poder validarla él mismo;
`GestionRangos` comprueba el solapamiento porque un rango no conoce a los demás.

## Idioma

- **Dominio en español**: `Documento`, `RangoNumeracion`, `EmitirFactura`,
  `TomarSiguienteConsecutivo`. Sin acentos en identificadores ni en comentarios
  de código (sí en documentación markdown).
- **Andamiaje en inglés**: nombres de proyecto, `Program`, paquetes.
- Campos JSON en `camelCase`; valores de enum en `SNAKE_CASE_MAYUSCULA`.

## Errores

- Toda violación de regla de negocio es una `ExcepcionDominio` con un `Codigo`
  estable en MAYUSCULAS: `RANGO_AGOTADO`, `NOTA_EXCEDE_VALOR_FACTURA`.
- El manejador global las convierte en `409` con Problem Details (RFC 7807) más
  un campo `codigo`. `400` es solo error de forma; `404` solo recurso inexistente.
- Todo código nuevo se agrega a la tabla de `docs/05-contrato-api.md`.
- Los mensajes dicen qué SÍ se puede hacer, no solo qué falló.

## Pruebas

Tres proyectos: `Domain.Tests`, `Application.Tests`, `Integration.Tests`
(Testcontainers con PostgreSQL real, nunca base en memoria).

- Nombres de prueba en español, con guiones bajos, describiendo la regla:
  `Un_documento_terminado_no_vuelve_a_cambiar`.
- Cada prueba referencia en un comentario la regla que cubre (`RN-04`, `INV-DOC-03`).
- **Verificación por mutación, obligatoria para pruebas de concurrencia:** romper
  a propósito el mecanismo que la prueba dice cubrir y confirmar que se pone roja.
  Dejar constancia en la ADR correspondiente. Una prueba que pasa con el código
  roto es peor que no tenerla.
- Las pruebas no pueden depender del orden de ejecución: comparten base de datos
  dentro de una clase y xUnit no garantiza el orden.

## Base de datos

- **Leer siempre la migración generada antes de aplicarla.** Las convenciones de
  EF son suposiciones sobre el modelo y a veces están mal: ya pasó con una columna
  `Id1` inventada y con una secuencia que EF quiso generar en la base.
- Las restricciones de unicidad se duplican en la base a propósito (protegen de
  carreras). Las reglas de negocio NO se duplican en `CHECK`: eso crea dos fuentes
  de verdad.
- **Orden de bloqueos, para evitar interbloqueos:** documento referenciado primero,
  rango de numeración después. Cualquier caso de uso nuevo respeta ese orden.
- Bloqueo pesimista con `FOR UPDATE` escrito a mano; EF no lo expresa. Materializar
  con `ToListAsync`, nunca con `FirstOrDefaultAsync` (compone una subconsulta).

## Git

- Conventional Commits, en español, sin acentos en el asunto.
- Una rama por hito: `hito/N-nombre`. PR con cuerpo que explique las decisiones,
  no solo los cambios. Squash merge.
- Los commits se agrupan por capa o por tema, no uno gigante por hito.
- El archivo del cuerpo del PR se borra antes de fusionar.

## Cómo trabajar en este proyecto

1. **Antes de escribir código, decir qué se va a hacer y esperar aprobación.**
2. **Cuando haya más de una opción razonable de diseño, preguntar antes de elegir.**
   Este proyecto existe para poder defender sus decisiones; una decisión tomada
   sin consultar es una decisión que su autor no puede explicar. Aplica a: dónde
   vive una regla, qué se bloquea y qué no, qué hereda un documento de otro, qué
   se expone por API y qué no.
3. Al cerrar un hito: actualizar `README.md` (estado y recorrido con curl),
   `docs/05-contrato-api.md`, `api/openapi.yaml` y la ADR afectada. **La
   documentación es parte del hito, no un extra.**
4. Si el código y el contrato escrito en la etapa 4 se contradicen, resolverlo
   decidiendo cuál de los dos estaba equivocado, y dejarlo explicado en el commit.
5. La deuda técnica conocida se anota y se paga en el hito donde empieza a doler,
   no antes ni mucho después.

## Entorno

- .NET 10, C# 14, PostgreSQL 18 en Docker.
- Windows con PowerShell. **No usar `< >` como marcadores de posición en comandos:
  PowerShell los interpreta como redirección.**
- `docker compose up -d db` antes de cualquier comando de `dotnet ef`.
- Los endpoints de desarrollo se registran por lista blanca de entornos
  (`Development`, `Testing`), nunca descartando `Production`.

## Estado

**Proyecto completo.** Los 9 hitos (H0 a H8) entregados y fusionados en `main`.
Despues, una auditoria final (rama `auditoria/revision-final`): 400 pruebas en
verde, 17 ADR. Lo que encontro y lo que quedo como deuda esta en
`docs/10-retrospectiva.md`, seccion 7.

No hay trabajo en curso. Cualquier cambio nuevo empieza por decidir si cabe en
el alcance declarado en `docs/01-vision-alcance.md`, y si no cabe, por ampliarlo
ahi primero.

### Antes de proponer nada, leer esto

- `docs/10-retrospectiva.md` — que se subestimo, que salio mejor de lo esperado
  y que se haria diferente. Es el resumen mas honesto del proyecto.
- `docs/09-trazabilidad.md` — cada requerimiento con su prueba, y los **tres que
  no tienen verificacion automatica**: RF-04 (el certificado entra por
  configuracion y no por un endpoint, a proposito), RNF-03 (el percentil 95
  nunca se ha medido) y RNF-08 (levantar con un comando se comprueba a mano).
- `docs/08-despliegue.md`, seccion 6 — que falta para un despliegue real.

### Lo que NO esta hecho, y es deliberado

- Todo corre contra un **simulador**. No se ha emitido nada ante la DIAN.
- El protocolo real es SOAP; solo existe la implementacion REST del simulador.
- Los codigos de municipio y unidad de medida no se validan contra las listas
  oficiales: se comprueba que vengan, no que existan.
- `FALLIDO` exige intervencion humana y no hay herramienta para ella.
- Un solo emisor. El modelo no lo impide, pero no esta implementado ni probado.
