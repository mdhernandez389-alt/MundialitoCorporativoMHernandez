# Requisitos de la prueba técnica — Mundialito Corporativo

Resumen de los requisitos de la prueba (Desarrollador .NET). Cada sección (§) es la referencia que usan
[ROADMAP.md](../ROADMAP.md) y [DECISIONES.md](DECISIONES.md).

**Contexto:** sistema para gestionar un torneo de fútbol corporativo: equipos, integrantes, calendario de
partidos, resultados, tabla de posiciones y goleadores. Si algo no está claro se define un criterio propio
sin perder de vista el requerimiento general. El frontend no se evalúa visualmente.

## §1. Objetivo

Solución completa aplicando: Clean Architecture, CQRS, EF Core (escritura), Dapper (lectura), Unit of Work,
Result Pattern, REST completo e idempotente, trazabilidad y observabilidad, frontend desacoplado y Docker.

## §2. Arquitectura (obligatoria)

Capas mínimas: Domain, Application, Infrastructure, Api.

- Domain no depende de ninguna otra capa.
- Application depende solo de Domain.
- Infrastructure implementa contratos.
- Api solo orquesta.

## §3. CQRS (obligatorio)

- **Commands:** EF Core, Unit of Work obligatorio, transacciones correctamente manejadas.
- **Queries:** exclusivamente Dapper, consultas optimizadas, sin EF para lectura, paginación en la base.

## §4. Unit of Work (obligatorio)

Centraliza transacciones, permite commit explícito, garantiza consistencia entre repositorios y se usa en
todos los Commands. No se permite guardar desde el DbContext fuera del Unit of Work.

## §5. Result Pattern (obligatorio)

Sin excepciones como control de flujo. `Result` y `Result<T>` con éxito/fallo, mensaje y código de error
opcional. La API mapea a 200, 201, 400, 404 y 409.

## §6. REST + idempotencia (obligatorio)

- GET lectura · POST creación · PUT reemplazo completo (idempotente) · PATCH parcial · DELETE idempotente.
- POST con header `Idempotency-Key`: la key se persiste, no se duplican registros y una repetición
  devuelve la misma respuesta original.
- Se evalúa el manejo de conflictos y estados HTTP.

## §7. Observabilidad y trazabilidad (obligatorio)

- **Logs:** `ILogger` o Serilog, estructurados, sin datos sensibles, niveles correctos (Debug, Information,
  Warning, Error).
- **Tracing:** CorrelationId por request, middleware que agrega TraceId, duración de cada request, logs
  enlazados al TraceId. Valorable: OpenTelemetry con exportación a Jaeger, Zipkin o similar.
- **Eventos de dominio:** como mínimo al crear un equipo y al registrar un resultado; deben registrarse,
  loggearse y mantener trazabilidad. No hace falta un bus de eventos externo.

## §8. Requisitos funcionales

- **Equipos:** CRUD completo, filtros, paginación, orden dinámico.
- **Jugadores:** registro por equipo, filtros, paginación.
- **Partidos:** creación, registro de resultado, filtros por fecha, equipo y estado, paginación.
- **Torneo:** tabla automática ordenada por puntos → diferencia de gol → goles a favor; goleadores;
  consultas optimizadas con Dapper.

## §9. Seed (obligatorio)

Automático al iniciar si la base está vacía: 4 equipos, 5 jugadores por equipo, 6 partidos (3 con
resultado). En Infrastructure, funcionando con Docker y permitiendo probar la tabla de inmediato.

## §10. Filtros y paginación (obligatorio)

Todos los GET: `pageNumber`, `pageSize`, `sortBy`, `sortDirection` y filtros por entidad. Respuesta:

```json
{ "data": [], "pageNumber": 1, "pageSize": 10, "totalRecords": 50, "totalPages": 5 }
```

No se permite traer todo a memoria.

## §11. Frontend Next.js (obligatorio)

Equipos, partidos, registro de resultados, tabla de posiciones, goleadores, filtros + paginación y manejo
de errores. Arquitectura limpia y desacoplada.

## §12. Docker (obligatorio)

Dockerfile de la API, Dockerfile del frontend y `docker-compose.yml` con API, base de datos y frontend.
Debe levantarse con `docker-compose up`.

## §13. Diagrama de arquitectura (obligatorio)

Capas, CQRS, EF Core (write), Dapper (read), Unit of Work, almacenamiento de idempotencia, eventos, logs,
flujo de traza, frontend y Docker.

## §14. Colección de Postman (obligatoria)

Endpoints organizados; casos de idempotencia, filtros, paginación y errores; variables de entorno.

## §15. Pruebas unitarias (obligatorias)

Mínimo: tabla de posiciones, cálculo de puntos, diferencia de gol, Result Pattern, idempotencia y
comportamiento del commit del Unit of Work. Valorable: tests de integración.

## §16. Criterios de evaluación

| Criterio | Peso |
|---|---|
| Diseño y Clean Architecture | 15% |
| CQRS correcto | 10% |
| EF Core (write) | 10% |
| Dapper (read) | 10% |
| Unit of Work | 10% |
| REST + idempotencia | 10% |
| Observabilidad (logs + traces) | 10% |
| Lógica del torneo | 10% |
| Filtros y paginación | 10% |
| Tests | 5% |

**Modalidad:** repositorio público en GitHub.
