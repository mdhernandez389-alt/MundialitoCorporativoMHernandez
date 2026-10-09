# Roadmap — Mundialito Corporativo

Construcción paso a paso de la prueba técnica. Cada paso es pequeño, se entiende y se explica antes de pasar al siguiente.

- **Estado:** ✅ hecho · 🔜 siguiente · ⬜ pendiente
- **Decisiones:** las ya tomadas, con su justificación completa, están en [docs/DECISIONES.md](docs/DECISIONES.md).
- **Req.:** sección de los requisitos ([docs/ENUNCIADO.md](docs/ENUNCIADO.md)) que cubre el paso.

> **⏸ Pendiente al retomar (paso 10):** completar `OfficialOrder` en
> [GetStandingsHandler.cs](backend/src/Mundialito.Infrastructure/Queries/Standings/GetStandingsHandler.cs)
> (busca `TODO(human)`): el `ORDER BY` del `RANK()` con los tres criterios del §8.
> Mientras esté vacío, `GET /api/standings` falla con 500. Después: probar tabla y goleadores, y cerrar el paso 10.

| # | Paso | Req. | Estado |
|---|---|---|---|
| 1 | Elegir arquitectura | §2, §3 | ✅ |
| 2 | Esqueleto de la solución (4 proyectos + referencias) | §2 | ✅ |
| 3 | Domain: Result Pattern | §5 | ✅ |
| 4 | Domain: entidades, reglas de negocio y eventos | §7, §8 | ✅ |
| 5 | Application: contratos CQRS, IUnitOfWork, repositorios | §3, §4 | ✅ |
| 6 | Infrastructure (escritura): EF Core, UnitOfWork, migraciones | §3, §4 | ✅ |
| 7 | Commands: equipos, jugadores, partidos, resultados | §3, §8 | ✅ |
| 8 | Api: controllers y mapeo Result → HTTP | §5, §6 | ✅ |
| 9 | Queries con Dapper: filtros, paginación, ordenamiento | §3, §10 | ✅ |
| 10 | Lógica del torneo: tabla de posiciones y goleadores | §8 | 🔜 |
| 11 | Idempotencia en POST (Idempotency-Key) | §6 | ⬜ |
| 12 | Observabilidad: logs, CorrelationId/TraceId, tracing, eventos | §7 | ⬜ |
| 13 | Seed de datos | §9 | ⬜ |
| 14 | Pruebas unitarias | §15 | ⬜ |
| 15 | Frontend Next.js | §11 | ⬜ |
| 16 | Dockerización | §12 | ⬜ |
| 17 | Colección de Postman | §14 | ⬜ |
| 18 | Diagrama de arquitectura y README | §13 | ⬜ |
| 19 | Publicar en GitHub | Modalidad | ⬜ |

---

## ✅ 1. Elegir arquitectura

| Decisión | Elegido |
|---|---|
| Arquitectura | Clean Architecture en 4 capas (impuesta por la prueba) |
| Organización de Application | Por feature y, dentro, `Commands/` y `Queries/` |
| Despachador CQRS | Interfaces propias `ICommandHandler` / `IQueryHandler` (sin MediatR) |
| Estilo de endpoints | Controllers |
| Base de datos | SQL Server |

## ✅ 2. Esqueleto de la solución

`Domain ← Application ← Infrastructure ← Api`. Las referencias entre proyectos hacen cumplir las reglas de dependencia.

## ✅ 3. Domain: Result Pattern

`Result`, `Result<T>`, `Error` y `ErrorType` en `Domain/Results/`. Ninguna regla de negocio usa excepciones como control de flujo.

| Decisión | Elegido |
|---|---|
| Código de error | Obligatorio (`"Team.NotFound"`) |
| 400 / 404 / 409 | `ErrorType` (`Validation`, `NotFound`, `Conflict`) dentro de `Error`; la Api traduce |
| Creación | Métodos explícitos + conversión implícita solo desde `Error` |

## ✅ 4. Domain: entidades, reglas y eventos

`Team`, `Player`, `Match` (con sus `Goal`), `TeamStanding`; eventos `TeamCreated` y `MatchResultRegistered`.

| Decisión | Elegido |
|---|---|
| Ids | `Guid` generado por el dominio (v7) |
| Marcador vs. goles | El marcador se deriva de la lista de goleadores |
| Corrección de resultado | `PUT` idempotente: mismo resultado → sin cambios; distinto → corrige y emite evento con el marcador anterior |
| Tabla de posiciones | Guardada (`TeamStanding`) y actualizada en la misma transacción que el resultado |

## ✅ 5. Application: contratos

`ICommand` / `IQuery` y sus handlers en `Abstractions/Messaging/`, `IUnitOfWork` en `Abstractions/Persistence/`, un repositorio por feature (`ITeamRepository`, `IPlayerRepository`, `IMatchRepository`, `ITeamStandingRepository`).

| Decisión | Elegido |
|---|---|
| UoW y repositorios | Se inyectan por separado; comparten el `DbContext` scoped por request |
| `CommitAsync` | Devuelve `Result`: conflictos de BD → `Error.Conflict` (409); lo inesperado → excepción (500) |
| Repositorios | Solo rastrean (`Add`, `Remove`, `Get...`); ninguno guarda |

## ✅ 6. Infrastructure (escritura)

`MundialitoDbContext` (`internal`), configuraciones con Fluent API, `UnitOfWork` con transacción explícita y traducción de errores de BD a `Result`, 4 repositorios, `AddInfrastructure()` y migración `InitialCreate`.

**Verificado:** la migración se aplica en SQL Server; el error 2601 trae el nombre del índice (`UX_Teams_Name`) y la collation trata "Sistemas" = "sistemas".

| Decisión | Elegido |
|---|---|
| `SaveChanges` fuera del UoW | DbContext `internal` + `SaveChanges` bloqueado; solo el UoW guarda |
| Concurrencia | Optimista con `rowversion` (Teams, Matches, TeamStandings) → 409 |
| Eventos de dominio | Tabla `DomainEvents` en la misma transacción + log después del commit |

## ✅ 7. Commands

7 Commands con sus handlers: `CreateTeam`, `UpdateTeam`, `PatchTeam`, `DeleteTeam`, `RegisterPlayer`, `ScheduleMatch`, `RegisterMatchResult`. Servicio de dominio `StandingsUpdater`, `AddApplication()` (registro por reflexión) y migración `TeamDeleteCascadesPlayers`.

**Verificado:** `StandingsUpdater` con 4 escenarios (primer registro, reintento idéntico, dos correcciones encadenadas): "Jugados" queda siempre en 1.

| Decisión | Elegido |
|---|---|
| Qué se puede eliminar | Equipos sin partidos (jugadores y fila de tabla en cascada); con partidos → 409 |
| PATCH | `null` = no cambiar (JSON Merge Patch) |
| Tabla de posiciones | Servicio de dominio `StandingsUpdater`, en la misma transacción que el resultado |

## ✅ 8. Api: controllers y mapeo de errores

`ApiController` (base: `Error` → ProblemDetails, `ErrorType` → status), `TeamsController`, `PlayersController`, `MatchesController`, requests en `Contracts/`. `Program.cs` con ProblemDetails para todo error (incluidos JSON inválido, rutas inexistentes y 500).

| Endpoint | Éxito |
|---|---|
| `POST /api/teams` | 201 + `Location` |
| `PUT` / `PATCH` / `DELETE /api/teams/{id}` | 204 |
| `POST /api/teams/{teamId}/players` | 201 + `Location` |
| `POST /api/matches` | 201 + `Location` |
| `PUT /api/matches/{id}/result` | 204 |

**Verificado:** 30 llamadas reales contra la API y SQL Server (éxitos, 400/404/409, DELETE repetido, ruta inexistente), todas con el status y `code` esperados. En la base: tabla correcta tras registrar, reintentar y corregir; cascada al borrar; eventos en `DomainEvents` y en el log.

**Base de desarrollo:** contenedor `mundialito-mh-db`, puerto `14333`.

| Decisión | Elegido |
|---|---|
| Formato de error | ProblemDetails (RFC 9457) + `code`, en todos los errores |
| DELETE de algo que no existe | 204 siempre (idempotente) |
| PUT / PATCH exitoso | 204 sin cuerpo |

## ✅ 9. Queries con Dapper

Queries y DTOs en Application; handlers con Dapper en `Infrastructure/Queries/` sobre piezas comunes (`SortMap`, `SqlWhere`, `PagedSql`, `DbConnectionFactory`). Seis GET nuevos:

| Endpoint | Filtros | `sortBy` |
|---|---|---|
| `GET /api/teams` | `name`, `department` | `name`, `department` |
| `GET /api/teams/{teamId}/players` | `name` | `shirtNumber`, `fullName` |
| `GET /api/matches` | `from`, `to`, `teamId`, `status` | `scheduledAt`, `status` |
| `GET` por Id (equipo, jugador, partido con goles) | — | — |

**Verificado:** 36 llamadas reales: paginación (incluida una página más allá del final), límites de `pageSize`, orden en mayúsculas/minúsculas, intentos de inyección en `sortBy`, `sortDirection` y filtros (400 o búsqueda literal; la tabla intacta), `%` literal, filtros de partidos por fecha/equipo/estado y forma exacta de la respuesta del §10.

| Decisión | Elegido |
|---|---|
| `sortBy` seguro | Lista blanca (`SortMap`) + `Id` como desempate; valor desconocido → 400 |
| Handlers de lectura | Query y DTO en Application; handler con Dapper y SQL en Infrastructure |
| Total de registros | 2 consultas en 1 viaje (`QueryMultiple`) |

## 🔜 10. Lógica del torneo

Tabla ordenada por puntos, diferencia de gol y goles a favor; goleadores. Ambas consultas optimizadas con Dapper.

| Decisión | Elegido |
|---|---|
| Orden fijo (§8) vs `sortBy` (§10) | Columna `position` con el orden oficial (default) + `sortBy` para otras vistas |
| Empate total | Posición compartida (`RANK`) |

**Estado:** contratos, handlers (`GetStandingsHandler`, `GetScorersHandler`) y endpoints (`GET /api/standings`, `GET /api/scorers`) escritos.
Falta el `TODO(human)` de `OfficialOrder` y las pruebas.

## ⬜ 11. Idempotencia

Header `Idempotency-Key`, key persistida, sin registros duplicados, misma respuesta ante una repetición.

**Decisiones a tomar:**
- ¿Middleware o filtro de acción?
- Misma key con distinto contenido: ¿409 o 422?
- Dos requests con la misma key al mismo tiempo.

## ⬜ 12. Observabilidad

Logs estructurados, CorrelationId y TraceId por request, duración, eventos de dominio loggeados. Opcional valorable: OpenTelemetry con exportación a Jaeger.

**Decisiones a tomar:**
- ¿Solo `ILogger` o Serilog?
- ¿Dónde se ven los logs y los traces? (Consola, Seq, Jaeger.)

## ⬜ 13. Seed

4 equipos, 5 jugadores por equipo, 6 partidos, 3 con resultado. En Infrastructure, automático si la base está vacía.

## ⬜ 14. Pruebas unitarias

Mínimo: tabla de posiciones, cálculo de puntos, diferencia de gol, Result Pattern, idempotencia y comportamiento de commit del UnitOfWork.

**Decisiones a tomar:**
- ¿Con qué base se prueba el UnitOfWork? (Provider InMemory, SQLite o SQL Server real.)

## ⬜ 15. Frontend Next.js

Equipos, partidos, registro de resultados, tabla, goleadores, filtros y paginación, manejo de errores.

## ⬜ 16. Docker

Dockerfile de la API, Dockerfile del frontend y `docker-compose.yml` con API, base de datos y frontend.

## ⬜ 17. Postman

Endpoints organizados; casos de idempotencia, filtros, paginación y errores; variables de entorno.

## ⬜ 18. Diagrama y README

Diagrama con capas, CQRS, EF/Dapper, UnitOfWork, idempotencia, eventos, logs, traza, frontend y Docker.

## ⬜ 19. GitHub

Repositorio público con el código, la documentación y la colección.

---

### Peso de cada criterio en la evaluación (§16)

| Criterio | Peso | Pasos |
|---|---|---|
| Diseño y Clean Architecture | 15% | 1, 2, 5 |
| CQRS correcto | 10% | 5, 7, 9 |
| EF Core (escritura) | 10% | 6 |
| Dapper (lectura) | 10% | 9, 10 |
| Unit of Work | 10% | 5, 6 |
| REST + Idempotencia | 10% | 8, 11 |
| Observabilidad | 10% | 12 |
| Lógica del torneo | 10% | 4, 10 |
| Filtros y paginación | 10% | 9 |
| Tests | 5% | 14 |
