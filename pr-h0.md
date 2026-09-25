Cierra el hito **H0 - Fundación**.

## Qué entrega

Un esqueleto ejecutable. Este hito no implementa ningún requerimiento del negocio, y eso es deliberado: mitiga el riesgo R-03 aprendiendo la plataforma sin reglas de dominio encima.

- Solución con 5 proyectos de código y 3 de pruebas, con las dependencias dirigidas hacia adentro.
- `GET /health`, con tres pruebas de integración.
- `docker-compose` con la API y PostgreSQL 18.
- Integración continua que compila y ejecuta las pruebas.
- `.gitignore`, `.gitattributes`, `.dockerignore`, `.env.example` y README.
- Spike de firma digital, ya retirado.

## Requerimientos cubiertos

| Requerimiento | Cómo se cumple |
|---|---|
| RNF-08 | `docker compose up` levanta el sistema completo en una máquina limpia |
| RNF-01 (parcial) | Sin secretos en el repositorio; configuración por variables de entorno |

## Demostración

```
docker compose up --build
curl http://localhost:8080/health
→ {"estado":"ok","momento":"2026-09-25T21:33:12.8682477+00:00"}

dotnet test
→ total: 3; con errores: 0; correcto: 3
```

## Riesgo retirado: R-05 (firma digital)

El spike confirmó que la firma es viable:

- Paquete `System.Security.Cryptography.Xml`. No viene en el framework base.
- `SignedXml` con `XmlDsigEnvelopedSignatureTransform`. Sin esa transformación la firma se calcula sobre sí misma y nunca verifica.
- El certificado se incrusta con `KeyInfoX509Data`.
- Verificado: firma válida, e **inválida tras alterar un byte del documento**.

El código del spike se borró, como estaba previsto. El hallazgo queda registrado aquí y en su issue.

## Qué quedó deliberadamente fuera

- Entidades de dominio, endpoints del negocio, tablas → H1
- Autenticación → H1
- Catálogos → H2
- XML, firma y transmisión → H5 a H7

## Lo que no estaba previsto

Cuatro cosas que la planeación no anticipó:

1. **PostgreSQL 18 cambió dónde monta sus datos.** El volumen va en `/var/lib/postgresql`, no en `/var/lib/postgresql/data` como en versiones anteriores. El contenedor diagnosticó su propio problema y explicó la corrección.
2. **.NET 10 genera `.slnx` en lugar de `.sln`.** Formato nuevo de solución, en XML, que produce menos conflictos al fusionar ramas. Hubo que ajustar el `.gitattributes`.
3. **`System.Security.Cryptography.Xml` es un paquete aparte.** No está en el framework base desde .NET Core.
4. **Las pruebas de integración necesitan que `Program` sea público.** Con instrucciones de nivel superior el compilador genera una clase interna que `WebApplicationFactory` no alcanza a ver. Se resuelve con `public partial class Program;` al final del archivo.

## Verificación de la definición de terminado

- [x] Los requerimientos del hito están implementados
- [x] Las pruebas pasan en integración continua
- [x] `docker compose up` levanta el sistema desde cero
- [x] La demostración se ejecuta y produce el resultado esperado
- [x] La documentación afectada está actualizada
