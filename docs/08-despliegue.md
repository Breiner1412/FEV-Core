# Guía de despliegue

Cómo levantar FEV-Core y qué se le configura. Cubre RNF-01 (ningún secreto en el repositorio) y la lista completa de variables que el código lee de verdad — no de memoria: salieron de buscar en el código qué claves consulta.

> **Antes de nada.** Este proyecto opera contra un **simulador** del servicio de validación, no contra la DIAN. La sección 6 dice exactamente qué falta para un despliegue real, y no es poco.

---

## 1. Levantarlo entero

Lo único que hace falta es Docker.

```bash
git clone https://github.com/Breiner1412/FEV-Core.git
cd FEV-Core
cp .env.example .env
docker compose up --build
```

Arranca tres contenedores:

| Servicio | Qué es | Puerto |
|---|---|---|
| `db` | PostgreSQL 18 | 5432 |
| `simulador` | Servicio de validación simulado | 5108 |
| `api` | FEV-Core | 8080 |

La API aplica las migraciones al arrancar y, fuera de producción, crea un integrador de desarrollo cuya llave aparece en los registros:

```bash
docker compose logs api | grep "Llave de API"
```

Comprobación rápida: `curl http://localhost:8080/health`.

---

## 2. Variables de entorno

En docker compose se leen del archivo `.env`. Fuera de compose, son variables de entorno normales o cualquier otra fuente de configuración de .NET.

**Sobre el doble guion bajo.** `Firma__CertificadoBase64` en una variable de entorno es lo mismo que `Firma:CertificadoBase64` en configuración. .NET traduce `__` a `:` porque los dos puntos no son válidos en variables de entorno de todos los sistemas.

### 2.1 Base de datos

| Variable | Obligatoria | Por defecto | Para qué |
|---|---|---|---|
| `POSTGRES_DB` | Sí | — | Nombre de la base. Lo usan el contenedor de PostgreSQL y la cadena de conexión. |
| `POSTGRES_USER` | Sí | — | Usuario. |
| `POSTGRES_PASSWORD` | Sí | — | Contraseña. |
| `ConnectionStrings__Principal` | Fuera de compose | — | Cadena completa. Dentro de compose se arma con las tres de arriba. |

### 2.2 Firma digital (RF-17)

| Variable | Obligatoria | Por defecto | Para qué |
|---|---|---|---|
| `Firma__CertificadoBase64` | Para firmar | vacío | El `.p12` o `.pfx` en base 64. **Nunca un archivo en el repositorio.** |
| `Firma__Clave` | Para firmar | vacío | Contraseña del certificado. |

Sin estas dos el sistema arranca igual y falla al firmar con `CERTIFICADO_NO_CONFIGURADO`. Es deliberado: se puede levantar el proyecto y recorrerlo entero sin tener un certificado real.

Para convertir un certificado:

```bash
base64 -w0 cert.p12                                              # Linux y macOS
[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.p12"))   # PowerShell
```

La vigencia se comprueba en **cada** firma, no al arrancar: un certificado que vence a medianoche deja de firmar a medianoche, no en el siguiente reinicio *(INV-CER-01)*.

### 2.3 Ambiente DIAN

| Variable | Obligatoria | Por defecto | Para qué |
|---|---|---|---|
| `Dian__Ambiente` | No | `Pruebas` | `Pruebas` o `Produccion`. |

Entra en el cálculo del código único, así que un documento de pruebas y uno de producción con los mismos datos producen códigos distintos. El valor por defecto es `Pruebas` a propósito: equivocarse hacia producción es el error caro.

### 2.4 Servicio de validación (RF-18, RF-19)

| Variable | Obligatoria | Por defecto | Para qué |
|---|---|---|---|
| `Validacion__UrlBase` | No | `http://localhost:5108` | Dónde vive el servicio. En compose apunta al simulador por su nombre de servicio. |
| `Validacion__TiempoEsperaSegundos` | No | `30` | Cuánto se espera una respuesta antes de darla por perdida. |

El tiempo de espera es la frontera entre "tardó mucho" y "no se sabe si llegó" *(ADR-0014)*. Bajarlo produce documentos en `FALLIDO` por culpa de un servicio simplemente lento.

### 2.5 Bandeja de salida (RNF-04, RNF-05)

| Variable | Obligatoria | Por defecto | Para qué |
|---|---|---|---|
| `Salida__Habilitado` | No | `true` | Apagarlo deja los documentos en `RECIBIDO` indefinidamente. Solo para depurar. |
| `Salida__IntervaloSondeoSegundos` | No | `2` | Cada cuánto mira el trabajador si hay algo que hacer. |
| `Salida__MaximoIntentos` | No | `5` | Cuántas veces se intenta antes de rendirse. Al agotarse, el documento pasa a `FALLIDO`. |
| `Salida__EsperaMaximaSegundos` | No | `300` | Techo de la espera creciente. Sin él, el intento quince esperaría nueve horas. |
| `Salida__TiempoDeAbandonoSegundos` | No | `120` | Cuánto puede estar tomada una tarea antes de considerarla abandonada *(ADR-0013)*. |

El último es un compromiso, no una garantía. Demasiado corto y dos trabajadores procesan la misma tarea; demasiado largo y un reinicio deja documentos parados.

### 2.6 Solo fuera de producción

| Variable | Por defecto | Para qué |
|---|---|---|
| `LLAVE_DESARROLLO` | `fev_desarrollo_no_usar_en_produccion` | Llave del integrador que se siembra al arrancar. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` y `Testing` activan los endpoints de desarrollo y el documento OpenAPI generado. |

Los endpoints de desarrollo se registran con una **lista blanca** de entornos, no descartando `Production`. Si alguien desplegara con el entorno llamado `Prod` o `produccion`, una lista negra lo dejaría pasar; la blanca falla del lado seguro.

---

## 3. Sin Docker

Hace falta el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0) y un PostgreSQL accesible.

```bash
export ConnectionStrings__Principal="Host=localhost;Database=fevcore;Username=fevcore;Password=..."

dotnet run --project src/FevCore.DianSimulator   # una terminal
dotnet run --project src/FevCore.Api             # otra
```

---

## 4. Migraciones

La API las aplica al arrancar salvo en `Testing`, donde las pruebas controlan cuándo. Para aplicarlas a mano:

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/FevCore.Infrastructure --startup-project src/FevCore.Api
```

---

## 5. Qué no se guarda nunca (RNF-01)

Ningún secreto vive en el repositorio. Concretamente:

- **El certificado de firma** entra por configuración en base 64. El repositorio no contiene ningún `.p12` ni `.pfx`, y las pruebas generan certificados autofirmados en memoria.
- **Las llaves de API** se guardan como huella criptográfica, nunca en claro. Una llave se muestra una sola vez, al crearse *(INV-INT-01)*.
- **La clave técnica** de un rango entra y no vuelve a salir: no aparece en ninguna respuesta de consulta.
- **`.env`** está en `.gitignore`. `.env.example` lleva los nombres, nunca los valores.

---

## 6. Qué falta para un despliegue real

Esta sección existe porque un despliegue de verdad no es este comando con otra contraseña.

- **Habilitación ante la DIAN.** Exige un proceso formal, un certificado emitido por entidad autorizada y superar un conjunto de pruebas de la autoridad. Nada de eso se ha hecho.
- **El protocolo real es SOAP.** `IProveedorValidacion` existe para que cambiar del simulador al servicio real sea escribir otra implementación, pero esa implementación no está escrita.
- **Los códigos de municipio y unidad de medida no se validan contra las listas oficiales.** Se comprueba que vengan, no que existan. Un código inventado pasaría por aquí y lo rechazaría la DIAN.
- **`FALLIDO` exige intervención humana y no hay herramienta para ella.** Hay que mirar la base de datos.
- **Sin límite de peticiones por integrador, sin métricas, sin alertas.** Relevante en un despliegue real, no en un ejercicio.
- **Un solo emisor.** El modelo no lo impide, pero no está implementado ni probado.

---

## 7. Solución de problemas

| Síntoma | Causa probable |
|---|---|
| `CERTIFICADO_NO_CONFIGURADO` al firmar | Faltan `Firma__CertificadoBase64` o `Firma__Clave`. |
| Los documentos se quedan en `RECIBIDO` | El trabajador está apagado (`Salida__Habilitado`) o el simulador no responde. |
| Los documentos acaban en `FALLIDO` | El servicio de validación no contesta. Con el simulador, comprueba su modo en `GET /salud`. |
| `RANGO_NO_DISPONIBLE` al emitir | No hay rango vigente para ese tipo y esa fecha *(RN-02)*. |
| La API no arranca y habla de la conexión | PostgreSQL todavía no está listo, o la cadena de conexión apunta a otro sitio. |

Toda respuesta de error trae un `traceId`. Es lo que permite encontrar la petición en los registros.
