# FEV-Core

API de emisión de documentos electrónicos para Colombia, construida sobre .NET 10.

Recibe los datos de una operación comercial y produce un documento electrónico generado, firmado y transmitido para validación, con su estado rastreable en todo momento. Está pensada para integrarse a un sistema que ya existe — un ERP, un e-commerce, un punto de venta — sin imponerle interfaz ni modelo de datos.

> **Estado: en construcción.** Hito 4 de 8 completado. Ver la [hoja de ruta](#hoja-de-ruta).

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

### Emitir una factura

En desarrollo se crea un integrador con una llave conocida, que aparece en los registros al arrancar:

```bash
docker compose logs api | grep "Llave de API"
```

Guarda la llave en una variable para no repetirla:

```bash
LLAVE="fev_desarrollo_no_usar_en_produccion"
API="http://localhost:8080/api/v1"
```

**1. Configura el emisor** (una sola vez):

```bash
curl -X PUT $API/emisor -H "X-Api-Key: $LLAVE" -H "Content-Type: application/json" -d '{
  "datos": {
    "tipoIdentificacion": "31",
    "identificacion": "800197268",
    "digitoVerificacion": "4",
    "razonSocial": "Comercializadora del Eje SAS",
    "direccion": "Calle 20 # 8-45",
    "municipioCodigo": "66001",
    "regimen": "48",
    "responsabilidades": ["O-13"]
  }
}'
```

El dígito de verificación se valida: si no corresponde al NIT, responde `409`.

**2. Registra el rango de numeración autorizado** (una sola vez):

```bash
curl -X POST $API/rangos-numeracion -H "X-Api-Key: $LLAVE" -H "Content-Type: application/json" -d '{
  "prefijo": "SETP",
  "tipoDocumento": "FACTURA",
  "numeroInicial": 1,
  "numeroFinal": 5000,
  "vigenteDesde": "2026-01-01",
  "vigenteHasta": "2027-01-01",
  "numeroAutorizacion": "18760000001",
  "claveTecnica": "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c"
}'
```

Sin un rango vigente no se emite nada: la factura del paso 4 respondería `409` con
código `RANGO_NO_DISPONIBLE`. Es la resolución de la DIAN la que autoriza los números,
no el sistema.

La clave técnica entra y no vuelve a salir: no aparece en esta respuesta ni en ninguna
consulta. `GET /rangos-numeracion` informa cuántos números quedan y cuántos días
faltan para el vencimiento.

**3. Registra un adquirente y un producto**, y guarda los `id` que devuelven:

```bash
curl -X POST $API/adquirentes -H "X-Api-Key: $LLAVE" -H "Content-Type: application/json" -d '{
  "datos": {
    "tipoIdentificacion": "13",
    "identificacion": "1088123456",
    "razonSocial": "Juan Perez",
    "direccion": "Carrera 10 # 5-20",
    "municipioCodigo": "66001",
    "regimen": "49"
  }
}'

curl -X POST $API/productos -H "X-Api-Key: $LLAVE" -H "Content-Type: application/json" -d '{
  "codigo": "PROD-001",
  "descripcion": "Teclado mecanico",
  "unidadMedida": "94",
  "precioUnitario": 150000,
  "impuestos": [{ "tipo": "IVA", "tarifa": 19 }]
}'
```

**4. Emite la factura** referenciando ambos:

```bash
curl -X POST $API/facturas -H "X-Api-Key: $LLAVE" -H "Content-Type: application/json" -d '{
  "referenciaExterna": "VTA-001",
  "adquirenteId": "<id del adquirente>",
  "lineas": [{ "productoId": "<id del producto>", "cantidad": 2 }]
}'
```

Responde `202` con `totalAPagar: 357000` y el número `SETP-1`, tomado del rango. La descripción, la unidad, el precio y el IVA se copiaron del catálogo.

**5. Compruébalo.** Cambia el precio del producto a 180.000 con un `PUT /productos/{id}`, y vuelve a consultar la factura:

```bash
curl $API/documentos/<id> -H "X-Api-Key: $LLAVE"
```

Sigue diciendo 150.000 y 357.000. Una factura emitida es una fotografía de un acuerdo, no una consulta viva.

Reenviar la petición del paso 4 con la misma `referenciaExterna` devuelve `200` y el mismo documento, sin crear otro ni consumir un número nuevo.

**6. Emite una nota crédito.** Una nota solo corrige una factura **aprobada**
(RN-03), y la transmisión real llega en H7, así que fuera de producción hay un
endpoint que recorre la máquina de estados a mano:

```bash
DOC=<pega aqui el id de la factura>

for ESTADO in EN_PROCESO TRANSMITIDO APROBADO; do
  curl -X POST $API/desarrollo/documentos/$DOC/estado     -H "X-Api-Key: $LLAVE" -H "Content-Type: application/json"     -d "{\"estado\": \"$ESTADO\", \"motivo\": \"Avance manual\"}"
done

curl -X POST $API/notas-credito -H "X-Api-Key: $LLAVE" -H "Content-Type: application/json" -d '{
  "referenciaExterna": "NC-001",
  "documentoReferenciadoId": "'"$DOC"'",
  "motivo": "DEVOLUCION_PARCIAL",
  "lineas": [{ "productoId": "<id del producto>", "cantidad": 1 }]
}'
```

Responde `202` con prefijo `NCA`: la nota usa su propio rango de numeración, no
el de facturas. No lleva adquirente porque lo hereda de la factura.

Emitir una segunda nota por más de lo que queda responde `409` con código
`NOTA_EXCEDE_VALOR_FACTURA` (RN-04). Referenciar una nota en lugar de una
factura responde `409 DOCUMENTO_REFERENCIADO_INVALIDO` (RN-05).

> El endpoint `/desarrollo/...` se registra solo en los entornos `Development` y
> `Testing`, mediante lista blanca. No aparece en `api/openapi.yaml`: el contrato
> describe lo que un integrador puede usar, y esto no lo es.

**7. Mira el historial** (RF-23):

```bash
curl $API/documentos/$DOC/historial -H "X-Api-Key: $LLAVE"
```

Devuelve las cuatro transiciones, empezando por el nacimiento del documento, que
tiene `estadoAnterior` nulo. Cada una dice cuándo ocurrió y por qué.

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
- **[Orden de bloqueos](docs/adr/0010-orden-de-bloqueos.md)** — cómo se evita un interbloqueo por diseño, y por qué aquí el bloqueo no protege la experiencia sino la verdad del dato.

---

## Hoja de ruta

| Hito | Qué entrega | Estado |
|---|---|---|
| H0 | Esqueleto ejecutable, CI, docker-compose | Completo |
| H1 | Emitir y consultar una factura de punta a punta | Completo |
| H2 | Emisor, adquirentes y productos | Completo |
| H3 | Numeración correcta bajo concurrencia | Completo |
| H4 | Notas crédito y débito, máquina de estados | Completo |
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
