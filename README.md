# FEV-Core

API de emisión de documentos electrónicos para Colombia, construida sobre .NET 10.

Recibe los datos de una operación comercial y produce un documento electrónico generado, firmado y transmitido para validación, con su estado rastreable en todo momento. Está pensada para integrarse a un sistema que ya existe — un ERP, un e-commerce, un punto de venta — sin imponerle interfaz ni modelo de datos.

> **Estado: completo.** Los 9 hitos entregados, 361 pruebas en verde. Ver la [hoja de ruta](#hoja-de-ruta).
>
> Si vas a mirar una sola cosa, que sea la [retrospectiva](docs/10-retrospectiva.md): qué se subestimó, qué salió mejor de lo esperado y qué haría diferente.

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

Se levantan tres contenedores: la base de datos, la API y el **simulador** del
servicio de validación, que responde en `http://localhost:5108`. La API le habla
sola; el puerto queda expuesto para poder cambiarle el modo durante el recorrido
de abajo.

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

**6. Míralo avanzar solo.** Nadie tiene que empujarlo. Un trabajador en segundo
plano toma el documento de la bandeja de salida, genera su XML, lo firma y lo
transmite al simulador; después consulta el veredicto hasta obtenerlo. Consulta
el mismo documento un par de veces con unos segundos de diferencia:

```bash
DOC=<pega aqui el id de la factura>

curl $API/documentos/$DOC -H "X-Api-Key: $LLAVE"
```

El `estado` recorre `RECIBIDO` → `EN_PROCESO` → `TRANSMITIDO` → `APROBADO`, y
aparece un `identificadorSeguimiento`. El campo `historial` guarda cada paso con
su motivo, así que se puede reconstruir qué pasó y cuándo.

**Prueba a romperlo.** El simulador obedece cuatro modos, y son la razón de que
exista:

```bash
SIM=http://localhost:5108

curl -X PUT $SIM/simulador/configuracion -H "Content-Type: application/json" -d '{"modo":"RECHAZA"}'
curl -X PUT $SIM/simulador/configuracion -H "Content-Type: application/json" -d '{"modo":"CAIDO"}'
curl -X PUT $SIM/simulador/configuracion -H "Content-Type: application/json" -d '{"modo":"SIN_RESPUESTA"}'
curl -X PUT $SIM/simulador/configuracion -H "Content-Type: application/json" -d '{"modo":"APRUEBA"}'
```

Con `RECHAZA`, el documento acaba en `RECHAZADO` y `erroresValidacion` trae los
motivos. Con `CAIDO`, el trabajador reintenta esperando cada vez más, y vuelve a
avanzar en cuanto el simulador se recupera. Con `SIN_RESPUESTA` —el que importa—
el documento acaba en `FALLIDO`, que **no** significa rechazado: significa que
nadie sabe si la DIAN lo recibió, y que hace falta una persona antes de volver a
emitir. El porqué está en el [ADR-0014](docs/adr/0014-resultado-desconocido.md).

**7. Emite una nota crédito.** Una nota solo corrige una factura **aprobada**
(RN-03). Si el simulador está en `APRUEBA`, la factura del paso 4 llega sola a
ese estado y no hace falta nada más. Si prefieres no levantar el simulador, fuera
de producción hay un endpoint que recorre la máquina de estados a mano:

```bash
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

**8. Genera el XML y descárgalo** (RF-16, RF-25):

```bash
curl -X POST $API/documentos/$DOC/xml -H "X-Api-Key: $LLAVE"
curl $API/documentos/$DOC/xml -H "X-Api-Key: $LLAVE" -o factura.xml
```

El primero calcula el CUFE, guarda el XML y mueve el documento a `EN_PROCESO`.
El segundo devuelve el archivo. Repetir el `POST` responde `409`: un documento
se representa de una sola forma, porque en la etapa 6 esos bytes concretos se
firman.

El XML valida contra el esquema oficial de UBL 2.1, que viene versionado en
`schemas/ubl-2.1/`. **Eso significa que su estructura es correcta, no que la
DIAN lo aceptaría:** el anexo técnico añade centenares de validaciones de
negocio que ningún esquema expresa. Qué campos se implementaron y cuáles no
está en [docs/07-cobertura-ubl.md](docs/07-cobertura-ubl.md).

**9. Firma el documento** (RF-17):

```bash
curl -X POST $API/documentos/$DOC/firma -H "X-Api-Key: $LLAVE"
curl $API/documentos/$DOC/xml -H "X-Api-Key: $LLAVE" -o factura-firmada.xml
```

Hace falta un certificado configurado en `.env` (ver `Firma__CertificadoBase64`).
Sin él responde `409 CERTIFICADO_NO_CONFIGURADO`, y el resto del recorrido
funciona igual: se puede levantar el proyecto entero sin tener uno.

La firma va en formato **XAdES-EPES**, dentro de `ext:UBLExtensions`, con la
política de firma que exige la DIAN. El XML descargado a partir de aquí es el
firmado; el original se conserva internamente para poder reproducir el cálculo.

Firmar no cambia el estado: el documento sigue en `EN_PROCESO`, que es
justamente lo que ese estado significa. Repetir el `POST` responde `409`, porque
una firma vale para unos bytes concretos.

**Lo que esto no garantiza:** que la DIAN aceptaría la firma. La estructura
sigue el anexo técnico y el documento valida contra el esquema, pero no se ha
emitido contra el entorno de pruebas de la autoridad, y el certificado de las
pruebas es autofirmado. Ver [ADR-0012](docs/adr/0012-firma-xades.md).

Para detener: `docker compose down`

### Para desarrollar

Con el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0):

```bash
dotnet build          # compilar
dotnet test           # ejecutar las pruebas
dotnet run --project src/FevCore.DianSimulator   # en una terminal
dotnet run --project src/FevCore.Api             # en otra
```

El simulador escucha en `http://localhost:5108`, que es adonde apunta la API por
defecto. Sin él, los documentos se quedan reintentando y acaban en `FALLIDO`.

Las pruebas de integración levantan PostgreSQL en contenedores con
Testcontainers, así que necesitan Docker en marcha. Las de generación de XML
validan contra los esquemas de UBL 2.1, que vienen **versionados** en
`schemas/ubl-2.1/`: al clonar ya están, y no hace falta descargar nada. El
script `scripts/descargar-esquemas-ubl.ps1` queda como registro de su
procedencia y para poder regenerarlos.

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
| — | [Decisiones (ADR)](docs/adr/) | 14 decisiones con sus alternativas descartadas |
| 5 | [Contrato de la API](docs/05-contrato-api.md) | Endpoints, códigos de error y guía de integración |
| — | [Especificación OpenAPI](api/openapi.yaml) | El contrato en formato procesable |
| 6 | [Plan de entregas](docs/06-plan-entregas.md) | Los 9 hitos y su definición de terminado |
| 7 | [Cobertura UBL](docs/07-cobertura-ubl.md) | Qué elementos del anexo técnico se generan y cuáles no |
| 8 | [Guía de despliegue](docs/08-despliegue.md) | Variables de entorno, secretos y qué falta para un despliegue real |
| 9 | [Trazabilidad](docs/09-trazabilidad.md) | Cada requerimiento con su implementación y su prueba, y los tres que no tienen |
| 10 | [Retrospectiva](docs/10-retrospectiva.md) | Qué se subestimó, qué salió mejor de lo esperado y qué haría diferente |

### Decisiones que vale la pena mirar

- **[Emisión asíncrona](docs/adr/0005-emision-asincrona.md)** — por qué la API no espera a la DIAN, y qué problema resuelve eso.
- **[Bandeja de salida](docs/adr/0006-bandeja-de-salida.md)** — cómo se garantiza que ningún documento quede sin procesar aunque el proceso muera.
- **[Bloqueo en la numeración](docs/adr/0009-bloqueo-numeracion.md)** — por qué una secuencia de base de datos no sirve para numeración fiscal.
- **[Firma XAdES-EPES](docs/adr/0012-firma-xades.md)** — cómo se construye XAdES sobre lo que .NET sí trae, y los dos errores de canonicalización que costaron encontrar.
- **[Generación del XML](docs/adr/0011-generacion-xml.md)** — por qué el UBL se escribe a mano, y qué garantiza (y qué no) validar contra el esquema oficial.
- **[Orden de bloqueos](docs/adr/0010-orden-de-bloqueos.md)** — cómo se evita un interbloqueo por diseño, y por qué aquí el bloqueo no protege la experiencia sino la verdad del dato.
- **[Resultado desconocido](docs/adr/0014-resultado-desconocido.md)** — por qué "no sé si llegó" es un desenlace distinto de "falló", y qué pasa si se confunden.
- **[Toma de tareas](docs/adr/0013-toma-de-tareas.md)** — cómo varios trabajadores se reparten la bandeja sin pisarse y sin retener una conexión mientras esperan a un tercero.

---

## Hoja de ruta

| Hito | Qué entrega | Estado |
|---|---|---|
| H0 | Esqueleto ejecutable, CI, docker-compose | Completo |
| H1 | Emitir y consultar una factura de punta a punta | Completo |
| H2 | Emisor, adquirentes y productos | Completo |
| H3 | Numeración correcta bajo concurrencia | Completo |
| H4 | Notas crédito y débito, máquina de estados | Completo |
| H5 | Generación del XML en UBL 2.1 | Completo |
| H6 | Firma digital | Completo |
| H7 | Simulador y transmisión asíncrona | Completo |
| H8 | Listados, OpenAPI generado y cierre | Completo |

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
