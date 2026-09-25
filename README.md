# FEV-Core

API de emisión de documentos electrónicos para Colombia, construida sobre .NET 10.

Recibe los datos de una operación comercial y produce un documento electrónico generado, firmado y transmitido para validación, con su estado rastreable en todo momento. Está pensada para integrarse a un sistema que ya existe — un ERP, un e-commerce, un punto de venta — sin imponerle interfaz ni modelo de datos.

> **Estado: en construcción.** Hito 0 de 8 completado. Ver la [hoja de ruta](#hoja-de-ruta).

> **Limitación importante.** Este proyecto opera contra un **simulador** del servicio de validación, no contra el servicio real de la DIAN. Conectarse al servicio real exige un proceso de habilitación con certificado digital emitido por entidad autorizada. El XML se valida contra el esquema oficial, pero **no ha sido verificado contra la DIAN real**. Es un ejercicio técnico y no constituye asesoría tributaria ni legal.

---

## Qué resuelve

En Colombia la facturación electrónica opera bajo validación previa: la DIAN debe aprobar el documento antes de que se considere expedido. Eso implica construir el XML en formato UBL 2.1 con adendas colombianas, firmarlo digitalmente y transmitirlo, sabiendo que la validación tarda varios segundos y puede fallar.

Esos detalles no son el negocio de quien vende zapatos o software de inventarios. FEV-Core se encarga de ellos y expone una API REST sencilla.

## Qué NO hace

Declarado explícitamente para que el alcance sea claro:

- No tiene interfaz gráfica. Es un componente de integración.
- No envía el documento al adquirente. Eso es responsabilidad del sistema integrador.
- No maneja nómina electrónica, documento soporte ni documento equivalente POS.
- No genera la representación gráfica en PDF.
- No es un sistema contable, de inventarios ni de cartera.

---

## Cómo levantarlo

Necesitas [Docker](https://www.docker.com/products/docker-desktop/) y nada más.

```bash
git clone https://github.com/Breiner1412/FEV-Core.git
cd FEV-Core
cp .env.example .env     # en Windows: Copy-Item .env.example .env
docker compose up --build
```

Verifica que respondió:

```bash
curl http://localhost:8080/health
```

```json
{"estado":"ok","momento":"2026-09-25T21:33:12.8682477+00:00"}
```

Para detener: `docker compose down`

### Para desarrollar

Con el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0):

```bash
dotnet build          # compilar
dotnet test           # ejecutar las pruebas
dotnet run --project src/FevCore.Api
```

---

## Cómo está organizado

```
src/
├── FevCore.Domain/          Entidades, invariantes y reglas de negocio.
│                            No depende de ninguna biblioteca externa.
├── FevCore.Application/     Casos de uso. Declara qué necesita del exterior.
├── FevCore.Infrastructure/  Persistencia, firma, XML, cliente HTTP, trabajador.
├── FevCore.Api/             Controladores, autenticación, manejo de errores.
└── FevCore.DianSimulator/   Simulador del servicio de validación.

tests/
├── FevCore.Domain.Tests/        Reglas de negocio. Sin infraestructura.
├── FevCore.Application.Tests/   Casos de uso con dobles de prueba.
└── FevCore.Integration.Tests/   Ciclo completo contra servicios reales.
```

Las dependencias apuntan hacia adentro: `Domain` no referencia a nadie. Esa restricción no es una convención, es una restricción del compilador, y es lo que permite probar las reglas de negocio en milisegundos sin levantar base de datos.

---

## Documentación

El proyecto se construyó siguiendo un proceso documentado. Cada documento justifica al siguiente.

| # | Documento | Qué contiene |
|---|---|---|
| 1 | [Visión y alcance](docs/01-vision-alcance.md) | El problema, los criterios de éxito y lo que queda fuera |
| 2 | [Requerimientos](docs/02-requerimientos.md) | 25 funcionales, 13 reglas de negocio, 12 no funcionales |
| 3 | [Modelo de dominio](docs/03-modelo-dominio.md) | 12 entidades, 6 agregados, 33 invariantes |
| 4 | [Arquitectura](docs/04-arquitectura.md) | Capas, flujos y estrategia de pruebas |
| — | [Decisiones (ADR)](docs/adr/) | 9 decisiones con sus alternativas descartadas |
| 5 | [Contrato de la API](docs/05-contrato-api.md) | Endpoints, códigos de error y guía de integración |
| — | [Especificación OpenAPI](api/openapi.yaml) | El contrato en formato procesable |
| 6 | [Plan de entregas](docs/06-plan-entregas.md) | Los 9 hitos y su definición de terminado |

### Decisiones que vale la pena mirar

- **[Emisión asíncrona](docs/adr/0005-emision-asincrona.md)** — por qué la API no espera a la DIAN, y qué problema resuelve eso.
- **[Bandeja de salida](docs/adr/0006-bandeja-de-salida.md)** — cómo se garantiza que ningún documento quede sin procesar aunque el proceso muera.
- **[Bloqueo en la numeración](docs/adr/0009-bloqueo-numeracion.md)** — por qué una secuencia de base de datos no sirve para numeración fiscal.

---

## Hoja de ruta

| Hito | Qué entrega | Estado |
|---|---|---|
| H0 | Esqueleto ejecutable, CI, docker-compose | Completo |
| H1 | Emitir y consultar una factura de punta a punta | Pendiente |
| H2 | Emisor, adquirentes y productos | Pendiente |
| H3 | Numeración correcta bajo concurrencia | Pendiente |
| H4 | Notas crédito y débito, máquina de estados | Pendiente |
| H5 | Generación del XML en UBL 2.1 | Pendiente |
| H6 | Firma digital | Pendiente |
| H7 | Simulador y transmisión asíncrona | Pendiente |
| H8 | Listados, OpenAPI generado y cierre | Pendiente |

---

## Marco normativo de referencia

- Resolución DIAN 000165 de 2023 — regulación base del sistema de facturación electrónica
- Resolución DIAN 000202 de 2025 — requisitos, validaciones y estructura de los documentos
- Resolución DIAN 000227 de 2025 — consolidación normativa
- Artículo 616-1 del Estatuto Tributario — obligación de facturar

La normativa se actualiza con frecuencia. Cualquier uso real exige verificar la versión vigente del anexo técnico de la DIAN.

---

## Licencia

MIT

## Autor

Breiner Stiven Guisao Rodríguez — [github.com/Breiner1412](https://github.com/Breiner1412)
