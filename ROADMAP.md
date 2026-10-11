# Roadmap — Mundialito Corporativo

Construcción paso a paso de la prueba técnica. Cada paso es pequeño, se entiende y se explica antes de pasar al siguiente.

- **Estado:** ✅ hecho · 🔜 siguiente · ⬜ pendiente
- **Decisiones:** las ya tomadas, con su justificación completa, están en [docs/DECISIONES.md](docs/DECISIONES.md).
- **Req.:** sección de los requisitos ([docs/ENUNCIADO.md](docs/ENUNCIADO.md)) que cubre el paso.

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
| 10 | Lógica del torneo: tabla de posiciones y goleadores | §8 | ✅ |
| 11 | Idempotencia en POST (Idempotency-Key) | §6 | ✅ |
| 12 | Observabilidad: logs, CorrelationId/TraceId, tracing, eventos | §7 | ✅ |
| 13 | Seed de datos | §9 | ✅ |
| 14 | Pruebas unitarias | §15 | ✅ |
| 15 | Frontend Next.js | §11 | ✅ |
| 16 | Dockerización | §12 | ✅ |
| 17 | Colección de Postman | §14 | ✅ |
| 18 | Diagrama de arquitectura y README | §13 | ✅ |
| 19 | Publicar en GitHub | Modalidad | 🔜 |

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

## ✅ 10. Lógica del torneo

Tabla ordenada por puntos, diferencia de gol y goles a favor; goleadores. Ambas consultas optimizadas con Dapper.

| Decisión | Elegido |
|---|---|
| Orden fijo (§8) vs `sortBy` (§10) | Columna `position` con el orden oficial (default) + `sortBy` para otras vistas |
| Empate total | Posición compartida (`RANK`) |

`GET /api/standings` (lee `TeamStandings`; `position` con `RANK()` sobre `calc.Points DESC, calc.GoalDifference DESC, s.GoalsFor DESC`) y `GET /api/scorers` (`COUNT` de `Goals` por jugador con `RANK` por goles), ambos paginados con filtros y `sortBy`.

**Verificado:** escenario armado con dos equipos a 6 puntos donde el de más goles a favor tiene peor diferencia: queda 2º (gana la diferencia, como pide el §8). Empate total → posición compartida. `sortBy` y filtros no alteran la posición. Goleadores: conteo, orden, empates y filtro por equipo.

## ✅ 11. Idempotencia

Header `Idempotency-Key`, key persistida, sin registros duplicados, misma respuesta ante una repetición.

| Decisión | Elegido |
|---|---|
| Mecanismo | Filtro de acción `[Idempotent]` en los tres POST |
| Header | Obligatorio → 400 si falta |
| Misma key, otro contenido | 422 (borrador IETF) |
| Requests simultáneos | Reserva atómica (`INSERT ... WHERE NOT EXISTS` con bloqueo) → el segundo recibe 409 |

`IIdempotencyStore` (Application), `SqlIdempotencyStore` + tabla `IdempotencyRecords` (Infrastructure, migración `AddIdempotencyRecords`), `IdempotencyFilter` + `[Idempotent]` en los tres POST (Api).

**Verificado:** 20 comprobaciones contra la API y SQL Server: sin key / key larga → 400 sin crear nada; repetición → mismo 201, mismo id y `Location`, header `Idempotent-Replayed`; mismo contenido en otro orden → repetición; otro contenido u otro endpoint → 422; un 409 de negocio libera la key; **10 requests simultáneos con la misma key → un solo equipo** (201 ×5 con el mismo id, 409 ×5).

## ✅ 12. Observabilidad

Logs estructurados, CorrelationId y TraceId por request, duración, eventos de dominio loggeados. Opcional valorable: OpenTelemetry con exportación a Jaeger.

| Decisión | Elegido |
|---|---|
| Motor de logs | Serilog detrás de `ILogger<T>` |
| Dónde se ven | Seq (logs) + Jaeger vía OpenTelemetry (trazas); consola siempre |
| CorrelationId vs TraceId | Separados y enlazados: en cada log, en los headers de respuesta y en cada evento de dominio guardado |

`CorrelationIdMiddleware` (headers `X-Correlation-Id` / `X-Trace-Id`), Serilog con log por request (duración y nivel según status), OpenTelemetry (ASP.NET Core + SqlClient → OTLP), `traceId` de los ProblemDetails unificado, eventos de dominio con `TraceId`/`CorrelationId` (migración `AddTraceabilityToDomainEvents`) y marcados en la traza.

**Verificado:** 22 comprobaciones siguiendo un mismo TraceId por headers, ProblemDetails, consola, tabla `DomainEvents`, Seq y Jaeger (request + 5 spans SQL + evento de dominio). CorrelationId inválido o largo → se genera otro. Niveles: 2xx INF, 4xx WRN, Debug del UnitOfWork solo en desarrollo.

**Contenedores de desarrollo:** `mundialito-mh-seq` (http://localhost:5341) y `mundialito-mh-jaeger` (http://localhost:16686).

## ✅ 13. Seed

4 equipos, 5 jugadores por equipo, 6 partidos, 3 con resultado. En Infrastructure, automático si la base está vacía.

| Decisión | Elegido |
|---|---|
| Cómo se crean los datos | Dominio + UnitOfWork, un solo commit (tabla calculada con la regla real, eventos registrados) |
| Migraciones | Al iniciar, con reintentos mientras SQL Server arranca |
| Fechas | Relativas al día del seed (jugados en el pasado, programados en el futuro) |

`DatabaseInitializer` (`IHostedService`: reintentos → migraciones → seed) y `DataSeeder` (dominio + UnitOfWork, un commit).

**Verificado desde cero:** base borrada y API arrancada con SQL Server apagado → 6 reintentos (servidor apagado y luego arrancando) → 4 migraciones → seed con 7 eventos de dominio → recién entonces la API escucha. 4 equipos × 5 jugadores, 3 partidos jugados en el pasado y 3 programados en el futuro; la tabla muestra el desempate por diferencia de gol. Al reiniciar con datos, no vuelve a sembrar.

## ✅ 14. Pruebas unitarias

Mínimo: tabla de posiciones, cálculo de puntos, diferencia de gol, Result Pattern, idempotencia y comportamiento de commit del UnitOfWork.

| Decisión | Elegido |
|---|---|
| Base para el UnitOfWork y la idempotencia | SQL Server real con Testcontainers |
| Framework | xUnit + `Assert` |
| Estructura | `Mundialito.UnitTests` (sin base) + `Mundialito.IntegrationTests` (Docker) |

**56 tests en verde:** 42 unitarios (Result Pattern, puntos, diferencia de gol, `StandingsUpdater`, `Match`, `Team`, `IdempotencyFilter`) y 14 de integración contra SQL Server real (commit/rollback del UnitOfWork, `SaveChanges` bloqueado, duplicado → 409, eventos con TraceId, concurrencia con rowversion, reserva atómica de idempotencia, orden oficial de la tabla con sus desempates).

```bash
cd backend
dotnet test tests/Mundialito.UnitTests          # segundos, sin Docker
dotnet test tests/Mundialito.IntegrationTests   # requiere Docker
```

## ✅ 15. Frontend Next.js

Equipos, partidos, registro de resultados, tabla, goleadores, filtros y paginación, manejo de errores.

| Decisión | Elegido |
|---|---|
| Dónde se llama a la API | Servidor de Next (Server Components + Server Actions); sin CORS |
| Organización | Capa `lib/api` tipada que devuelve `ApiResult<T>` |
| Alcance | Lo del §11 + crear equipo (muestra idempotencia y errores 409) |

Next.js 16 (App Router, Cache Components) en `frontend/`: tabla (`/`), goleadores, equipos (+ crear), equipo con jugadores, partidos con filtros, detalle con registrar/corregir resultado. Capa `lib/api` (`server-only`), filtros/orden/página en la URL, `ErrorAlert` con code y traceId.

**Verificado contra la API real** (build de producción): tabla y desempate del seed, goleadores, filtros, orden y paginación; errores 400 de la API mostrados con code y traceId; equipo inexistente → "No encontrado"; **doble envío del formulario con la misma Idempotency-Key → un solo equipo** (la API registra la repetición); nombre duplicado → 409 en el formulario; API caída → "No se pudo conectar" sin romper la página.

```bash
cd frontend
npm install
API_URL=http://localhost:5092 npm run dev    # http://localhost:3000
```

## ✅ 16. Docker

Dockerfile de la API, Dockerfile del frontend y `docker-compose.yml` con API, base de datos y frontend (más Seq y Jaeger).

| Decisión | Elegido |
|---|---|
| Servicios | db, api, frontend, seq, jaeger: todo con un comando |
| Contraseña de SQL Server | Valor de demo por defecto + `.env` opcional (`.env.example`) |
| Arranque | Healthcheck de SQL Server + reintentos de la API |

```bash
docker-compose up --build
# Frontend http://localhost:3000 · API http://localhost:5000 · Seq http://localhost:5341 · Jaeger http://localhost:16686
docker-compose down -v   # borrar todo (el próximo up vuelve a sembrar)
```

**Verificado desde cero:** migraciones y seed automáticos, API y frontend respondiendo, idempotencia, logs en Seq y trazas en Jaeger.

## ✅ 17. Postman

Endpoints organizados; casos de idempotencia, filtros, paginación y errores; variables de entorno.

| Decisión | Elegido |
|---|---|
| Organización | Carpetas por recurso (Equipos, Jugadores, Partidos, Torneo) + por caso del §14 (Idempotencia, Filtros y paginación, Errores) |
| Tests | En cada request (status + `code`), ejecutables con *Run collection* o `newman` |
| Entornos | Docker (`http://localhost:5000`) y Local (`http://localhost:5092`) |

**Verificado con newman:** 50 requests, 165 aserciones, 0 fallos; repetible sobre la misma base.

```bash
npx newman run postman/Mundialito.postman_collection.json -e postman/Mundialito-Docker.postman_environment.json
# En PowerShell (Windows), si bloquea los scripts: usar npx.cmd en lugar de npx.
```

## ✅ 18. Diagrama y README

Diagrama con capas, CQRS, EF/Dapper, UnitOfWork, idempotencia, eventos, logs, traza, frontend y Docker.

| Decisión | Elegido |
|---|---|
| Formato | Mermaid en Markdown (GitHub lo dibuja; versionado como texto) |
| Vistas | Cinco diagramas enfocados en [docs/ARQUITECTURA.md](docs/ARQUITECTURA.md) + uno general en el README |

[README.md](README.md): levantar con un comando, cómo probar, arquitectura y tabla requisito → dónde se cumple.

## 🔜 19. GitHub

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
