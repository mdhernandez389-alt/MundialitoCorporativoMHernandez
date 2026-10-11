# Bitácora de decisiones

Cada decisión de diseño, con su motivo. Sirve para defender la solución en la entrevista.

## 1. Arquitectura: Clean Architecture en 4 capas (impuesto por la prueba)

```
Mundialito.Api  ──►  Mundialito.Infrastructure  ──►  Mundialito.Application  ──►  Mundialito.Domain
      └──────────────────────────────────────────────────►┘
```

| Proyecto | Referencia a | Por qué |
|---|---|---|
| Domain | (nadie) | El núcleo del negocio no depende de frameworks ni de la base de datos. |
| Application | Domain | Define los casos de uso y las **interfaces** que necesita (IUnitOfWork, repositorios). |
| Infrastructure | Application | **Implementa** esas interfaces (EF Core, Dapper). Inversión de dependencias. |
| Api | Application + Infrastructure | Es el *composition root*: arma todo con DI y solo orquesta. |

La regla la hace cumplir el **compilador**: si Domain intentara usar algo de Infrastructure, no compilaría porque no tiene la referencia.

## 2. Estructura de Application: feature + CQRS

```
Application/
  Abstractions/          ICommandHandler, IQueryHandler, IUnitOfWork...
  Teams/
    Commands/CreateTeam/ CreateTeamCommand.cs, CreateTeamHandler.cs
    Queries/GetTeams/    GetTeamsQuery.cs, TeamDto.cs
  Matches/ ...
```

**Por qué:** la prueba pide "separación clara" entre Commands y Queries. CQRS es una separación de *caminos*
(escritura: dominio + EF Core + UoW / lectura: SQL con Dapper a DTOs), no de carpetas; pero agrupar
por feature y dentro separar Commands/Queries hace la separación **visible** y mantiene junto todo lo de un recurso.

## 3. Despachador CQRS: interfaces propias (sin MediatR)

`ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>`; el controller recibe el handler por DI.

**Por qué:** cero "magia", se explica línea por línea. MediatR pasó a licencia comercial (v13+) y para este
tamaño no aporta. Si el proyecto creciera, se puede agregar un dispatcher o decorators sin tocar los handlers.

## 4. Endpoints: Controllers

**Por qué:** es el estándar en equipos .NET empresariales; `[ApiController]` da validación automática del
modelo (400) y binding de query strings a objetos, útil para filtros y paginación.

## 5. Base de datos: SQL Server

**Por qué:** es el motor habitual en banca con .NET. Paginación eficiente en la base con
`OFFSET ... ROWS FETCH NEXT ... ROWS ONLY`. Contra asumido: la imagen Docker es pesada y tarda en arrancar.

## 6. Result Pattern (§5)

Archivos en `Mundialito.Domain/Results/`: `Result`, `Result<T>`, `Error`, `ErrorType`.

```csharp
public Result<Team> Find(Guid id) =>
    team is null ? TeamErrors.NotFound : Result.Success(team);
```

| Decisión | Elegido | Por qué |
|---|---|---|
| Ubicación | Domain | Las propias entidades devuelven `Result` al validar reglas; Application y Api lo reutilizan. |
| Código de error | **Obligatorio** (`"Team.NotFound"`) | La prueba lo pide "opcional" como mínimo; obligatorio es más estricto y sirve al frontend (decide por código, no por texto), a los logs estructurados y a los tests. |
| 400 / 404 / 409 | `ErrorType` dentro de `Error` (`Validation`, `NotFound`, `Conflict`) | El dominio dice **qué** pasó sin saber de HTTP; la Api traduce con un único `switch`. Un error nuevo no obliga a tocar la Api. |
| Creación | `Result.Success(x)` / `Result.Failure<T>(e)` explícitos + conversión implícita **solo** desde `Error` | Se lee sin "magia"; el implícito permite `return TeamErrors.NotFound;`, el caso más común, sin ambigüedad. |
| `Error` | `record` sellado con constructor privado y fábricas | Igualdad por valor (útil en tests) y no se puede crear un error sin código, mensaje y tipo. |
| Éxito sin error | `Error.None` en vez de `null` | Evita chequeos de nulos; `Error` nunca es `null`. |

**¿Y las excepciones que quedan?** `Result` lanza en dos casos: crear `Failure(Error.None)` / éxito con error,
y leer `.Value` de un resultado fallido. No son control de flujo: son **bugs del programador** (como un
`ArgumentNullException`) y deben fallar ruidosamente. Los casos de negocio (no existe, duplicado, inválido)
siempre viajan como `Result`.

## 7. Dominio: entidades, reglas y eventos (§7, §8)

```
Domain/
  Abstractions/  Entity (Id + eventos), IDomainEvent, DomainEvent
  Results/       Result, Result<T>, Error, ErrorType
  Teams/         Team, TeamErrors, TeamCreatedDomainEvent
  Players/       Player, PlayerErrors
  Matches/       Match, Goal, Score, GoalScorer, MatchStatus, MatchErrors, MatchResultRegisteredDomainEvent
  Standings/     TeamStanding
```

| Decisión | Elegido | Por qué |
|---|---|---|
| Ids | `Guid` generado por el dominio (`Guid.CreateVersion7()`) | La entidad tiene identidad desde que nace: el evento `TeamCreated` y la URL del `201 Created` no dependen de la base. Contra: SQL Server ordena los `uniqueidentifier` por sus últimos bytes, así que v7 no evita del todo la fragmentación del índice; para este volumen es irrelevante. |
| Marcador vs. goles | **El marcador se deriva de los goles** | El request de resultado es la lista de goleadores; `Match` cuenta los de cada equipo. Es imposible que tabla y goleadores se contradigan: la regla no se valida, no se puede romper. Fuera de alcance (documentado): autogoles. |
| Corrección de resultado | `PUT` idempotente | El resultado es un sub-recurso que se reemplaza. Mismo resultado (aunque en otro orden) → sin cambios ni evento. Distinto → se reemplazan los goles y el evento lleva `PreviousScore`. |
| Tabla de posiciones | **Guardada** (`TeamStanding`) y actualizada en la misma transacción | Las reglas (3/1/0, diferencia de gol) viven en C# y se prueban con tests unitarios (§15). Actualizar resultado + 2 filas de tabla en una transacción demuestra el UoW con varios repositorios (§4). Dapper solo lee y ordena. Contra: dato derivado; lo protege la transacción. |
| `ApplyResult` / `RevertResult` | Un solo método `Tally(gf, gc, direction)` con `+1` / `-1` | Revertir resta exactamente lo que aplicar sumó; no hay dos versiones de la regla que puedan desincronizarse. |
| Relaciones | `Player` y `Match` referencian equipos solo por Id; `Goal` vive dentro de `Match` | Agregados pequeños: se paginan y filtran sin cargar grafos. `Match` es dueño de sus goles (solo `RegisterResult` los cambia). |
| Creación | Constructores privados + fábricas (`Create`, `Schedule`) que devuelven `Result` | No se puede crear una entidad inválida. EF Core puede usar el constructor privado con parámetros. |
| Reglas que requieren la base | En el handler (nombre único, dorsal libre, equipos/jugadores existentes) + índice único | La entidad valida lo que puede saber sola; lo que requiere consultar la base va en Application, y el índice único cubre la carrera entre dos requests. |

Eventos de dominio: `TeamCreatedDomainEvent` y `MatchResultRegisteredDomainEvent` (§7). La entidad solo los acumula
(`Raise`); registrarlos, loggearlos y despacharlos es trabajo de Infrastructure al hacer commit (pasos 6 y 12).

## 8. Application: contratos CQRS, Unit of Work y repositorios (§3, §4)

```
Application/
  Abstractions/
    Messaging/    ICommand, ICommand<T>, ICommandHandler, IQuery<T>, IQueryHandler
    Persistence/  IUnitOfWork
  Teams/          ITeamRepository
  Players/        IPlayerRepository
  Matches/        IMatchRepository
  Standings/      ITeamStandingRepository
```

| Decisión | Elegido | Por qué |
|---|---|---|
| Marcadores `ICommand` / `IQuery<T>` | Interfaces vacías que tipan cada mensaje | El compilador impide registrar una query como command. Si después hacen falta *decorators* (log, transacción), se aplican solo a un lado. |
| Respuesta de los handlers | `Result` / `Result<T>`, también en las queries | Una lectura también falla de forma esperada (`GET /teams/{id}` inexistente → 404). |
| UoW y repositorios | **Se inyectan por separado**; `IUnitOfWork` solo confirma | Cada handler declara lo que usa y el UoW no crece con cada repositorio. La consistencia entre repositorios (§4) la garantiza que todos comparten el mismo `DbContext` scoped por request. Contra: esa garantía depende del lifetime de DI, no se ve en la firma. Se cubre con un test del commit (paso 14). |
| `CommitAsync` | **Devuelve `Result`** | Una carrera entre dos requests (índice único, concurrencia) es un caso esperado → `Error.Conflict` → 409, sin excepción como control de flujo (§5). Lo inesperado (base caída) sí se propaga como excepción → 500. |
| Repositorios | Solo rastrean (`Add`, `Remove`, `Get...`); **ninguno guarda** | Así "guardar fuera del UnitOfWork" no es posible desde Application. |
| Ubicación de los repositorios | En Application, por feature | Application define lo que necesita y Infrastructure lo implementa (inversión de dependencias). |
| Lecturas en un Command | Con EF, a través del repositorio | Cargar el agregado para modificarlo o revisar una regla (nombre único) es parte del *write side*. La restricción "Dapper exclusivo" aplica a las Queries, que son lo que devuelve un GET. |
| Alcance de los contratos | Mínimo: solo los métodos que piden los Commands conocidos | El paso 7 agrega lo que exijan las reglas de eliminación (p. ej. "¿el equipo tiene partidos?"). |

## 9. Infrastructure (escritura): EF Core y Unit of Work (§3, §4, §7)

```
Infrastructure/
  DependencyInjection.cs        AddInfrastructure(): DbContext, UnitOfWork y repositorios (todo Scoped)
  Persistence/
    MundialitoDbContext.cs      internal, SaveChanges bloqueado
    UnitOfWork.cs               transacción + eventos + traducción de errores
    DomainEventRecord.cs        fila de la tabla DomainEvents
    Configurations/             Fluent API, una clase por tabla
    Repositories/               implementaciones de los contratos de Application
    Migrations/                 InitialCreate
```

| Decisión | Elegido | Por qué |
|---|---|---|
| Bloquear `SaveChanges` fuera del UoW | **`internal` + guardia** | El DbContext es `internal`, así que la Api no lo ve (barrera en compilación). Además `SaveChanges`/`SaveChangesAsync` lanzan excepción; solo el UoW guarda, vía `SaveChangesFromUnitOfWorkAsync` (`internal`). Llamarlo directo es un bug del programador y por eso puede lanzar (ver §6). Cumple el §4 al pie de la letra, incluso dentro de Infrastructure (seed). |
| Transacción | Explícita en `CommitAsync` (`BeginTransaction` → `SaveChanges` → `Commit`) | Un solo `SaveChanges` ya es atómico, pero la transacción explícita hace visible el §4 ("centralice las transacciones") y deja espacio para guardar más cosas en el mismo commit (idempotencia, paso 11). El rollback es implícito: si no se llega al commit, `await using` la descarta. |
| Concurrencia | **Optimista con `rowversion`** en `Teams`, `Matches` y `TeamStandings` | Sin esto, dos registros simultáneos del mismo resultado suman el partido **dos veces** a la tabla. Y dos partidos del mismo equipo registrados a la vez pierden una suma. Con `rowversion`, el segundo falla → rollback de todo → `PersistenceErrors.ConcurrencyConflict` (409). Como el PUT de resultado es idempotente, reintentar da 200. Es *shadow property*: el Domain no sabe que existe. |
| Eventos de dominio | **Tabla `DomainEvents` + log** | El UoW saca los eventos de las entidades rastreadas y los guarda como filas (tipo, JSON, fecha) **en la misma transacción**: el evento existe solo si el cambio se guardó. Se loggean **después** del commit, así no se loggea algo que terminó en rollback. Es la base de un Outbox; el procesador en segundo plano queda fuera de alcance. |
| Traducción de errores de BD | 2601/2627 (único) → error de dominio según el índice; 547 (FK) → `ReferenceConflict`; concurrencia → `ConcurrencyConflict`. Lo demás sigue como excepción → 500 | Solo se traduce lo que significa "chocaste con otro request". Los índices únicos tienen nombre explícito (`UX_Teams_Name`) y una tabla índice → error, así el cliente recibe `Team.NameDuplicated` aunque el duplicado venga de una carrera. Verificado contra SQL Server: el mensaje 2601 incluye el nombre del índice. |
| Ids | `ValueGeneratedNever()` | El Id lo genera el dominio. Sin esto EF supone que una entidad nueva que ya trae Id "ya existe" y hace UPDATE en vez de INSERT. |
| `Score` | `ComplexProperty` (EF Core 10, opcional) → columnas `HomeGoals` / `AwayGoals` nullables | Value object en la misma fila, sin tabla ni Id propios. `null` = partido no jugado. |
| `MatchStatus` | Guardado como texto (`"Played"`) | La base se entiende sola y los filtros de Dapper (paso 9) se leen claros. |
| Borrado | FKs entre agregados con `Restrict`; `Goals` y `TeamStandings` en `Cascade` | Lo que es parte del dueño (goles del partido, fila de tabla del equipo) se va con él. Entre agregados, la base no borra nada sola mientras el paso 7 no defina las reglas. |
| `Points` / `GoalDifference` | No se guardan | Son derivados. La tabla ordenada (paso 10) los calcula en el SQL de Dapper. |
| Nombre único sin distinguir mayúsculas | Collation por defecto de SQL Server (CI) | "Sistemas" y "sistemas" chocan, tanto en el chequeo del handler como en el índice. |
| Cadena de conexión | `ConnectionStrings:Mundialito`; la de desarrollo en `appsettings.Development.json` | La contraseña es solo del contenedor local. En Docker (paso 16) llega por variable de entorno. |

## 10. Commands (§3, §8)

| Command | Endpoint (paso 8) | Qué hace en una transacción |
|---|---|---|
| `CreateTeam` | `POST /teams` | Equipo + su fila en la tabla (en cero) + evento `TeamCreated` |
| `UpdateTeam` | `PUT /teams/{id}` | Reemplaza nombre y departamento |
| `PatchTeam` | `PATCH /teams/{id}` | Cambia solo los campos enviados |
| `DeleteTeam` | `DELETE /teams/{id}` | Borra equipo, jugadores y fila de tabla (si no tiene partidos) |
| `RegisterPlayer` | `POST /teams/{teamId}/players` | Jugador con dorsal único en su equipo |
| `ScheduleMatch` | `POST /matches` | Partido programado entre dos equipos existentes |
| `RegisterMatchResult` | `PUT /matches/{id}/result` | Goles + marcador + 2 filas de tabla + evento `MatchResultRegistered` |

| Decisión | Elegido | Por qué |
|---|---|---|
| Qué se puede eliminar | Solo `DELETE` de equipos (lo único que pide el §8), y **solo si no tiene partidos** → si no, `Team.HasMatches` (409) | Los rivales tienen puntos ganados contra él: borrarlo en cascada dejaría la tabla inconsistente. Sin partidos no hay goles, así que sus jugadores y su fila de tabla se borran en cascada (migración `TeamDeleteCascadesPlayers`). Si alguien programa un partido entre el chequeo y el borrado, la FK lo frena → `ReferenceConflict` (409). |
| PATCH | **`null` = no cambiar** (semántica de JSON Merge Patch, RFC 7396) | Simple para el frontend y Postman; no mete paquetes de ASP.NET en Application. El handler completa lo faltante con el valor actual y pasa por el mismo `Team.Update` que el PUT: una sola regla de validación. La limitación (no poder "borrar" un campo) no aplica: ambos son obligatorios. |
| Orden de validación | Entidad (400) → base (404/409) → commit | Lo barato y sin I/O primero. Excepción: en `RegisterPlayer` el equipo va en la **ruta**, así que si no existe se responde 404 antes de validar el body. |
| Tabla de posiciones | Servicio de dominio `StandingsUpdater.ApplyResultChange(home, away, previous, new)` | Regla pura del torneo que involucra dos filas y un marcador: va en Domain y se prueba sin base. El handler solo carga, llama y confirma. |
| Goleadores | Ids de jugador, uno por gol; se resuelven en **una** consulta (`GetByIdsAsync`) | El equipo de cada gol sale del jugador, no del cliente: es imposible acreditar un gol al equipo equivocado. |
| Fila de tabla faltante | Excepción (500), no `Result` | Cada equipo nace con su fila; que falte es un dato corrupto (un bug), no un caso de negocio. |
| Registro de handlers | `AddApplication()` recorre el ensamblado por reflexión | Un handler nuevo queda registrado sin tocar la DI. Los handlers son `internal`: la Api solo los usa a través de la interfaz. |

## 11. Api: controllers y traducción Result → HTTP (§5, §6)

```
Api/
  Program.cs              composition root, ProblemDetails, manejo de excepciones
  Controllers/
    ApiController.cs      base: Error → ProblemDetails, ErrorType → status code
    TeamsController.cs    POST / PUT / PATCH / DELETE  /api/teams
    PlayersController.cs  POST                         /api/teams/{teamId}/players
    MatchesController.cs  POST /api/matches · PUT /api/matches/{id}/result
  Contracts/              requests y CreatedResponse (el contrato HTTP, separado de los Commands)
```

| Situación | Respuesta |
|---|---|
| POST exitoso | `201 Created` + header `Location` + `{ "id": "..." }` |
| PUT / PATCH / DELETE exitoso | `204 No Content` |
| `ErrorType.Validation` | `400` |
| `ErrorType.NotFound` | `404` |
| `ErrorType.Conflict` (duplicado, concurrencia, equipo con partidos) | `409` |
| JSON mal formado, campo `[Required]` ausente | `400` (ASP.NET) con `code: "Request.Invalid"` |
| Excepción no controlada (bug) | `500` sin stack trace, con `code: "Server.Error"` |

| Decisión | Elegido | Por qué |
|---|---|---|
| Formato de error | **ProblemDetails (RFC 9457) + `code`** | Es el formato que ASP.NET ya usa para sus propios errores: con él el frontend maneja **uno** solo. Trae `traceId` para cruzar con los logs (§7). `CustomizeProblemDetails` garantiza que **todo** error lleve `code`, incluso los que no pasan por nuestro código. |
| DELETE de algo que no existe | **204 siempre** | El estado pedido ("ese equipo no existe") ya se cumple. Repetir el DELETE responde igual que la primera vez: idempotencia visible (§6). Así lo recomiendan las Microsoft/Azure REST API Guidelines. Lo decide el handler (Application), no un caso especial en la Api. Contra asumido: un Id mal escrito también da 204. |
| PUT / PATCH exitoso | **204 sin cuerpo** | CQRS: un Command cambia estado y no devuelve datos de lectura. El recurso actualizado se pide con GET (lado Query). |
| Requests separados de los Commands | `Contracts/` en la Api | El Id viene de la ruta y los datos del body. El contrato HTTP puede evolucionar sin tocar Application. |
| Validación | El dominio valida reglas; la Api solo exige lo que el dominio **no puede** detectar | Se apagó el `[Required]` implícito de ASP.NET para strings: un nombre ausente llega `null` y responde `Team.NameRequired`, con el mismo código que un nombre vacío. Los `Guid` y la fecha sí llevan `[Required]`: ausentes llegarían como `Guid.Empty` / `0001-01-01`, que parecen datos reales. |
| `scorerPlayerIds` | Obligatorio, `[]` = 0 a 0 | Un 0 a 0 se registra a propósito; olvidarse del campo no lo registra por accidente. |
| Fecha del partido | `DateTimeOffset` en el request → UTC en el dominio | El cliente manda su zona horaria y no hay ambigüedad sobre "las 18:00 de quién". |
| Handlers en el controller | `[FromServices]` por acción | Cada acción recibe solo el handler que usa; el controller no tiene un constructor con 4 dependencias. |
| HTTPS redirect | Quitado | La API corre detrás de Docker / el frontend por HTTP. TLS se termina afuera (proxy), no en el contenedor. |
| Base de desarrollo | Contenedor `mundialito-mh-db`, puerto `14333` | No choca con otro SQL Server local en el 1433. |

## 12. Queries con Dapper: filtros, paginación y orden (§3, §10)

```
Application/                                   (el QUÉ: contratos, sin SQL ni Dapper)
  Abstractions/Querying/   PagedQuery, PagedResult<T>, QueryErrors
  Teams/Queries/           TeamDto, GetTeams/GetTeamsQuery, GetTeamById/GetTeamByIdQuery
  Players/Queries/         PlayerDto, GetTeamPlayers/..., GetPlayerById/...
  Matches/Queries/         MatchDto, MatchDetailDto, GoalDto, GetMatches/..., GetMatchById/...
Infrastructure/Queries/                        (el CÓMO: Dapper + SQL)
  Common/                  DbConnectionFactory, SqlWhere, SortMap, PagedSql
  Teams/ Players/ Matches/ handlers de lectura
```

| Endpoint | Filtros | `sortBy` (por defecto en negrita) |
|---|---|---|
| `GET /api/teams` | `name`, `department` (contiene) | **`name`**, `department` |
| `GET /api/teams/{teamId}/players` | `name` (contiene) | **`shirtNumber`**, `fullName` |
| `GET /api/matches` | `from`, `to`, `teamId` (local o visitante), `status` | **`scheduledAt`**, `status` |
| `GET /api/teams/{id}`, `.../players/{id}`, `/api/matches/{id}` | — | — |

| Decisión | Elegido | Por qué |
|---|---|---|
| `sortBy` seguro | **Lista blanca** (`SortMap`): nombre público → columna SQL; desconocido → 400 con los valores permitidos | Dapper parametriza valores, no nombres de columna: `ORDER BY @SortBy` no existe. El texto del usuario solo se usa para *buscar* en el diccionario; lo que llega al SQL lo escribimos nosotros. Rechazar (en vez de ignorar) hace visible un typo del frontend. |
| Desempate | Siempre `, Id` al final del ORDER BY | Sin columna única, filas empatadas pueden cambiar de orden entre consultas y aparecer en dos páginas o en ninguna. |
| Dónde viven los handlers de lectura | **Query y DTO en Application; handler con SQL en Infrastructure** | Application sigue dependiendo solo de Domain (§2, sin Dapper). En lectura el SQL *es* la lógica, así que vive junto a la base. La Api solo conoce `IQueryHandler<TQuery, TResult>`. |
| Total de registros | **2 consultas, 1 viaje** (`QueryMultiple`: `COUNT` + página) | Cada SELECT hace una cosa. Con `COUNT(*) OVER()` una página vacía (más allá del final) no trae filas y por lo tanto tampoco el total. |
| Paginación | `OFFSET/FETCH` en SQL Server; `pageSize` entre 1 y **100**; `pageNumber` ≥ 1; inválido → 400 | El tope garantiza "nunca traer todo a memoria" (§10) incluso con `pageSize=1000000`. |
| WHERE dinámico | `SqlWhere` agrega **solo** los filtros presentes, siempre parametrizados | Evita `(@x IS NULL OR ...)`, que obliga a SQL Server a usar un plan genérico. Cada combinación de filtros tiene su plan. |
| Filtro "contiene" | `LIKE @p ESCAPE '\'` con `%`, `_` y `[` escapados | Buscar "50%" encuentra "50%", no todo lo que empiece con "50". |
| `status` | Comparado contra los nombres del enum, sin distinguir mayúsculas | `Enum.TryParse` aceptaría "1" o "99" como válidos. |
| Fechas en la respuesta | `ScheduledAtUtc` marcado como UTC en el DTO | SQL devuelve `datetime2` "sin zona"; sin marcarlo, el JSON no lleva `Z` y el cliente lo leería como hora local. |
| GET por Id | Un objeto, no una página | "Todos los GET" del §10 se interpreta como todos los **listados**: paginar un único recurso no tiene sentido. Son las URLs del `Location` de los POST. |
| Jugadores de un equipo inexistente | 404 (no lista vacía) | Una lista vacía haría creer que el equipo existe pero no tiene jugadores. |
| Detalle de partido | Partido + goles (con nombre del jugador) en un solo viaje | El frontend lo necesita para mostrar o corregir el resultado. |
| Binding | `[FromQuery]` directo a la Query de Application | Los parámetros del listado *son* la Query; un DTO intermedio sería una copia 1:1. En los Commands sí hay request propio porque combinan ruta + body. |

## 13. Lógica del torneo: tabla de posiciones y goleadores (§8)

| Endpoint | Fuente | Filtros | `sortBy` |
|---|---|---|---|
| `GET /api/standings` | `TeamStandings` (precalculada) | `teamName`, `department` | **`position`**, `points`, `goalDifference`, `goalsFor`, `teamName` |
| `GET /api/scorers` | `Goals` agrupados por jugador | `teamId`, `playerName` | **`position`**, `goals`, `playerName`, `teamName` |

| Decisión | Elegido | Por qué |
|---|---|---|
| Orden fijo (§8) vs `sortBy` (§10) | **Columna `position` + `sortBy`** | `position` se calcula en SQL con el orden oficial (puntos → diferencia → goles a favor) y es el orden por defecto. `sortBy` permite otras vistas, pero la posición de cada equipo no cambia. Se cumplen los dos requisitos sin contradecirse. |
| Empate total | **Posición compartida (`RANK`)**: 1, 2, 2, 4 | Los criterios del §8 no distinguen a esos equipos; desempatar por nombre inventaría una regla. Dentro del empate se listan por nombre solo para que la paginación sea estable. |
| Posición con filtros | Se calcula en una **subconsulta sobre todo el torneo**, antes del WHERE y la paginación | Filtrar por "Finanzas" muestra su posición real (p. ej. 3º), no "1º de 1". Lo mismo para goleadores filtrados por equipo. |
| Puntos y diferencia | Calculados en SQL con `CROSS APPLY`, usando `TeamStanding.PointsPerWin` / `PointsPerDraw` | La regla 3/1/0 vive en una sola constante del dominio; el SQL la interpola (es una constante del código, no texto del usuario). `CROSS APPLY` los calcula una vez para usarlos en el `RANK` y en el `SELECT`. |
| Tabla: ¿calcular o leer? | **Leer** `TeamStandings` | Ya se actualiza en la misma transacción que cada resultado (paso 4); el GET no recorre partidos: su costo depende de los equipos, no de los partidos jugados. |
| Goleadores: ¿calcular o guardar? | **Calcular** con `COUNT(*) … GROUP BY` sobre `Goals` | Los goles ya están normalizados e indexados por jugador. Guardar otro contador sería un dato derivado más que mantener sincronizado. Como el marcador se deriva de los goles, ambas tablas nunca se contradicen. |
| Goleadores sin goles | No aparecen | Una tabla de goleadores lista a quien anotó. |

## 14. Idempotencia en POST (§6)

```
Application/Abstractions/Idempotency/IIdempotencyStore.cs   contrato (reservar, completar, liberar)
Infrastructure/Idempotency/                                  tabla IdempotencyRecords + SqlIdempotencyStore
Api/Idempotency/                                             IdempotencyFilter + atributo [Idempotent]
```

Se aplica a los tres POST: `POST /api/teams`, `POST /api/teams/{teamId}/players` y `POST /api/matches`.
PUT, PATCH y DELETE ya son idempotentes por diseño (pasos 4, 7 y 8).

| Situación | Respuesta |
|---|---|
| Sin `Idempotency-Key` | `400` `Idempotency.KeyRequired` |
| Key de más de 100 caracteres | `400` `Idempotency.KeyTooLong` |
| Key nueva | Se ejecuta; si sale bien (2xx) se guarda la respuesta |
| Misma key, mismo contenido, ya completada | **La respuesta original** (mismo status, cuerpo y `Location`) + header `Idempotent-Replayed: true`; no se ejecuta nada |
| Misma key, **otro** contenido | `422` `Idempotency.KeyReused` |
| Misma key, request original todavía ejecutándose | `409` `Idempotency.RequestInProgress` |
| La acción falla (400/404/409 de negocio o excepción) | La reserva se libera: el cliente corrige y reintenta con la misma key |

| Decisión | Elegido | Por qué |
|---|---|---|
| Mecanismo | **Filtro de acción `[Idempotent]`** | Corre después del model binding: ve el body ya parseado y el resultado de la acción (status, body, `Location`) sin manipular streams. Es explícito: en el controller se ve qué endpoints son idempotentes. |
| Header | **Obligatorio** en los POST → 400 si falta | Garantiza que ningún POST pueda duplicar registros. El frontend genera un UUID por envío de formulario; Postman usa `{{$guid}}`. |
| Key reusada con otro contenido | **422** | Es lo que indica el borrador IETF `draft-ietf-httpapi-idempotency-key-header`. Separa "corrige tu request" (422) de "reintenta en un momento" (409). |
| Concurrencia | **Reserva atómica + 409** | `INSERT ... WHERE NOT EXISTS` con `UPDLOCK, HOLDLOCK` en una sola sentencia: de dos requests simultáneos, solo uno inserta, sin excepciones de por medio. El otro recibe 409 y, al reintentar, la respuesta guardada. |
| Huella del contenido | SHA-256 de método + ruta + body **ya parseado** | Un JSON con otros espacios o con los campos en otro orden cuenta como el mismo contenido. La ruta incluye los Ids (`/teams/{id}/players`). |
| Qué respuestas se guardan | Solo las **2xx** | Un 400/404/409 de negocio no consumió nada: liberar la key permite corregir y reintentar. Repetir un request fallido simplemente vuelve a fallar igual. |
| Persistencia | Tabla `IdempotencyRecords` (PK = key), creada por migración de EF, leída y escrita con **SQL directo** | No es un dato del dominio ni un Command: es un registro HTTP que debe confirmarse **antes** del Command (la reserva) y por separado de su transacción. La reserva atómica es una sola sentencia SQL que EF no puede expresar. No toca el DbContext, así que el §4 se mantiene. |
| Guardado tras el commit | `CancellationToken.None` | Si el Command ya se confirmó, la respuesta se guarda aunque el cliente haya cortado: ese cliente es justo el que va a reintentar. |
| Fallo entre el commit y el guardado (caída del proceso) | La key queda "en curso" → los reintentos reciben 409 | Falla del lado seguro: nunca duplica. Mejora posible (fuera de alcance): expirar reservas viejas. |
| Alcance de la key | Global (PK = key) | La misma key en otro endpoint cambia la huella (la ruta) → 422. |

## 15. Observabilidad: logs, CorrelationId/TraceId y trazas (§7)

```
Api/Observability/
  ObservabilityExtensions.cs   AddObservability(): Serilog + OpenTelemetry · UseObservability(): middleware + log por request
  CorrelationIdMiddleware.cs   X-Correlation-Id / X-Trace-Id, scope de logs, tag en la traza
Application/Abstractions/Observability/ICorrelationContext.cs
Infrastructure: UnitOfWork sella cada evento con TraceId + CorrelationId (columnas nuevas en DomainEvents)
```

**Flujo de un request:**

```
request ──► CorrelationIdMiddleware ── CorrelationId (del cliente o nuevo) + TraceId (W3C)
              │                        → scope de logs, tag en la traza, headers de respuesta
              ▼
            UseSerilogRequestLogging ── al terminar: "HTTP POST /api/teams respondió 201 en 38.2 ms"
              ▼
            ExceptionHandler → controller → handler → UnitOfWork
              │  cada log lleva CorrelationId + TraceId
              │  cada consulta SQL (EF Core y Dapper) es un span en la traza
              │  cada evento de dominio: fila con TraceId/CorrelationId + log + evento en la traza
              ▼
response: X-Correlation-Id, X-Trace-Id (y "traceId" en los ProblemDetails: el mismo valor)
```

| Decisión | Elegido | Por qué |
|---|---|---|
| Motor de logs | **Serilog**, detrás de `ILogger<T>` | El código de la aplicación no se acopla a Serilog. Aporta salida JSON estructurada, sinks (consola, Seq), configuración por appsettings y `UseSerilogRequestLogging` (una línea por request con duración). |
| Dónde se ven | **Seq** (logs) + **Jaeger** vía OpenTelemetry/OTLP (trazas) | Cubre el "opcional valorable" del §7. Desde un log en Seq se salta a la traza en Jaeger por el TraceId. Sin configurarlos, la API solo escribe a consola y sigue generando TraceIds. |
| CorrelationId vs TraceId | **Separados y enlazados** | TraceId = W3C de cada request (Jaeger, logs, ProblemDetails). CorrelationId = el del cliente en `X-Correlation-Id` (puede agrupar varios requests de una acción) o uno nuevo. Los dos van en cada log, en los headers de respuesta y en cada evento guardado. |
| `traceId` en ProblemDetails | Se reemplaza el de ASP.NET (`00-<trace>-<span>-01`) por el TraceId solo | Un único valor en header, body, logs, Seq y Jaeger: se copia y se busca tal cual. |
| CorrelationId del cliente | Solo si tiene ≤ 100 caracteres y únicamente letras, dígitos, `-`, `_`, `.` | Termina en logs y headers: así nadie puede inyectar saltos de línea en los logs ni headers falsos. Si no es válido se genera uno nuevo. |
| Orden del pipeline | Observabilidad → ExceptionHandler → resto | El log de la excepción (500) queda dentro del CorrelationId, y el log de duración no la duplica: ve el 500 ya resuelto. |
| Trazabilidad de eventos | Columnas `TraceId` y `CorrelationId` en `DomainEvents` + `ActivityEvent` en la traza | "Mantener trazabilidad" (§7): de un evento guardado se llega a su request, a sus logs y a su traza. |
| Datos sensibles | No se loggean bodies, query strings, headers (salvo los de correlación) ni la cadena de conexión. Las trazas SQL llevan el texto con parámetros (`@Name`), nunca sus valores | §7: "no loggear datos sensibles". |
| Niveles | Debug: detalle interno (cambios por commit). Information: requests exitosos, eventos, repeticiones idempotentes. Warning: commit rechazado por la base. Error: excepciones. El nivel del log de cada request depende de su status (ver `GetRequestLogLevel`). | §7: "niveles correctos". |
| Ruido | `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore` y `System.Net.Http` en Warning; `Mundialito` en Debug solo en desarrollo | Los logs muestran lo de la aplicación, no el detalle interno del framework. |
| Formato | Producción/Docker: JSON compacto. Desarrollo: texto legible con `corr=` y `trace=` | JSON para que lo procesen máquinas (Seq, `docker logs`); texto para leer en la terminal. |

## 16. Seed de datos (§9)

```
Infrastructure/Seeding/
  DatabaseInitializer.cs   IHostedService: espera a SQL Server, migra, siembra si está vacía
  DataSeeder.cs            4 equipos × 5 jugadores, 6 partidos (todos contra todos), 3 con resultado
```

| Requisito del §9 | Cómo se cumple |
|---|---|
| Automático al iniciar | `IHostedService`: corre antes de que la API acepte requests |
| Si la DB está vacía | "Vacía" = sin equipos (sin equipos no puede haber jugadores, partidos ni tabla) |
| 4 / 5 / 6 / 3 | Sistemas, Finanzas, Operaciones, Talento Humano; dorsales 9, 10, 8, 5, 1; todos contra todos; los 3 primeros jugados |
| En Infrastructure | `Infrastructure/Seeding/` |
| Funcionar con Docker | Migraciones al iniciar con reintentos: la API espera a que SQL Server esté listo |
| Probar la tabla de inmediato | La tabla se calcula con `StandingsUpdater` en el mismo commit del seed |

| Decisión | Elegido | Por qué |
|---|---|---|
| Cómo se crean los datos | **Dominio + UnitOfWork**, un solo `CommitAsync` | El mismo camino que la API: fábricas, `StandingsUpdater`, repositorios. Los datos no pueden ser inválidos, la tabla sale de la regla real y se registran los eventos (4 `TeamCreated` + 3 `MatchResultRegistered`). Además, la guardia del paso 6 impide guardar con el DbContext fuera del UoW. |
| Todo o nada | Un único commit | Si algo falla no queda una base "a medias" que el siguiente arranque daría por sembrada (porque ya tendría equipos). |
| Migraciones | **Al iniciar, con reintentos** (hasta 10, pausas de 2 a 10 s) | `docker-compose up` funciona desde cero sin ejecutar `dotnet ef`. Que SQL Server no responda al arrancar es una condición externa esperada; agotados los intentos, la API no arranca (mejor que arrancar sin base). EF Core usa un lock propio: dos instancias no migran a la vez. |
| Fechas | **Relativas al día del seed** (−14, −7, −3, +3, +7, +14 días, 18:00 UTC) | Siempre hay partidos pasados y próximos para los filtros por fecha, sin importar cuándo se pruebe. |
| Goleadores | Cada gol se asigna rotando entre el 9, el 10 y el 8 de su equipo | La tabla de goleadores tiene variedad y empates para mostrar la posición compartida. |
| Falla del seed | Excepción, no `Result` | Son datos fijos escritos por nosotros: si el dominio los rechaza es un bug y debe fallar ruidosamente. |
| Qué se reintenta | `SqlException` directo **o envuelto** por EF Core en `InvalidOperationException` | Encontrado al probar: con SQL Server apagado llega un `SqlException` (error 258); mientras **arranca** acepta la conexión y la corta en el handshake, y EF Core lo envuelve ("transient failure", error 10054). Solo reintentar el primero tumbaba la API justo en el caso de Docker. |
| Marcadores sembrados | Sistemas 3-1 Finanzas · Operaciones 4-3 Talento Humano · Sistemas 6-2 Operaciones | La tabla muestra un desempate del §8 apenas se levanta: Talento Humano y Finanzas a 0 puntos, separados por diferencia de gol (−1 vs −2). Los goleadores muestran una posición compartida (cuatro con 3 goles). |

## 17. Pruebas (§15)

```
backend/tests/
  Mundialito.UnitTests/          sin base de datos, milisegundos
    Domain/   ResultTests, TeamStandingTests, StandingsUpdaterTests, MatchTests, TeamTests
    Api/      IdempotencyFilterTests (almacén falso en memoria)
  Mundialito.IntegrationTests/   SQL Server real y desechable (Testcontainers)
    Persistence/  UnitOfWorkTests
    Idempotency/  SqlIdempotencyStoreTests
    Queries/      StandingsQueryTests
```

| Mínimo del §15 | Dónde se prueba |
|---|---|
| Tabla de posiciones | `StandingsUpdaterTests` (registrar, reintento, correcciones) + `StandingsQueryTests` (orden oficial en SQL, desempates, empate total, `sortBy` sin cambiar la posición) |
| Cálculo de puntos | `TeamStandingTests` (3/1/0, combinación) |
| Diferencia de gol | `TeamStandingTests` (positiva, negativa, cero, acumulada) |
| Result Pattern | `ResultTests` |
| Idempotencia | `IdempotencyFilterTests` (400/422/409, repetición sin ejecutar, liberación ante error, huella) + `SqlIdempotencyStoreTests` (ciclo de vida, 10 reservas simultáneas → una gana) |
| Commit del UnitOfWork | `UnitOfWorkTests` (varios repositorios en un commit, sin commit no se guarda, `SaveChanges` bloqueado, duplicado → 409 + rollback, eventos con TraceId/CorrelationId, concurrencia con rowversion) |

| Decisión | Elegido | Por qué |
|---|---|---|
| Base para integración | **SQL Server con Testcontainers** | Lo que se prueba es propio de SQL Server: transacciones, `rowversion`, códigos 2601/547, `UPDLOCK/HOLDLOCK`. InMemory no tiene transacciones ni índices únicos y SQLite usa otros códigos y no tiene `rowversion`: los tests pasarían sin probar lo importante. El contenedor se crea y se destruye solo: no depende del entorno de quien corre los tests (solo de Docker). |
| Framework | **xUnit + `Assert`** | El más usado en .NET; sin dependencias extra ni licencias (FluentAssertions pasó a licencia comercial en v8). |
| Estructura | **Unit + Integration** | `dotnet test tests/Mundialito.UnitTests` corre en segundos sin Docker; los de integración se corren aparte. |
| Nombres | En español, describiendo el comportamiento (`Una_correccion_revierte_...`) | Leer la lista de tests es leer las reglas del negocio. |
| Aislamiento | Un contenedor por corrida; cada test usa nombres únicos | Los tests comparten la base sin pisarse; no hace falta limpiar entre tests. |
| `internal` | `InternalsVisibleTo` solo para el proyecto de tests correspondiente | Se prueban `UnitOfWork`, `IdempotencyFilter` y el `DbContext` sin hacerlos públicos para el resto. |
| Valorable: integración | Sí: la tabla real (Dapper + `RANK()`), la concurrencia del UoW y la reserva atómica se prueban contra SQL Server | Son las partes donde un mock "daría verde" sin probar nada. |

## 18. Frontend Next.js (§11)

```
frontend/src/
  lib/api/        types.ts (contratos), client.ts (fetch → ApiResult), endpoints.ts (una función por endpoint)
  lib/            search-params.ts (filtros/orden/página en la URL), format.ts (fechas en hora de El Salvador)
  components/     ErrorAlert, Pagination, SortableHeader, FilterForm, TeamSelect, Nav
  app/
    page.tsx               Tabla de posiciones
    scorers/               Goleadores
    teams/                 Equipos (filtros, orden, paginación) + crear equipo (Server Action, Idempotency-Key)
    teams/[id]/            Equipo y sus jugadores
    matches/               Partidos (desde/hasta, equipo, estado)
    matches/[id]/          Detalle + registrar/corregir resultado (Server Action, PUT)
    loading.tsx · error.tsx · not-found.tsx
```

| Requisito del §11 | Dónde |
|---|---|
| Visualización equipos | `/teams`, `/teams/[id]` |
| Visualización partidos | `/matches`, `/matches/[id]` |
| Registro de resultados | `/matches/[id]` (registrar y corregir) |
| Tabla de posiciones | `/` |
| Goleadores | `/scorers` |
| Filtros + paginación | En todos los listados, con orden por columna |
| Manejo de errores | `ApiResult` + `ErrorAlert` (mensaje, código, traceId); `notFound()` para 404; `error.tsx` para lo inesperado |

| Decisión | Elegido | Por qué |
|---|---|---|
| Dónde se llama a la API | **Servidor de Next**: Server Components para leer, Server Actions para escribir | El navegador nunca habla con la API: sin CORS, la URL de la API no viaja al bundle (`API_URL` sin `NEXT_PUBLIC_`) y en Docker se usa la red interna. Las páginas llegan con los datos ya renderizados. |
| Organización | **Capa de cliente API** (`lib/api`) | Las páginas no hacen `fetch`: llaman funciones tipadas que devuelven `ApiResult<T>` (el Result Pattern del backend del lado del frontend). Si la API cambia, se toca `lib/api`; `server-only` impide importarla desde el navegador. |
| Filtros, orden y página | **En la URL** (`?status=Played&pageNumber=2`), con `next/form` (GET) y `<Link>` | Se pueden compartir y el "atrás" funciona; el servidor renderiza directamente lo pedido. Los filtros funcionan sin JavaScript. |
| Idempotency-Key | Generada en el servidor al mostrar el formulario (`connection()` + `crypto.randomUUID()`), oculta en el `<form>` | Un doble envío o un reintento reusa la misma key → la API devuelve el mismo equipo. Tras el éxito se redirige y el formulario nuevo trae otra key. Un error de negocio libera la key en la API, así que corregir y reenviar funciona. |
| Registro de resultado | Un campo de goles por jugador; la Server Action lo convierte en la lista de Ids que espera la API | El usuario piensa "Carlos hizo 2"; la API recibe `[carlos, carlos]`. El formulario viene precargado con el resultado actual: guardar sin cambios es idempotente y cambiar algo lo corrige. |
| Cache Components (Next 16) | Activo (default de la plantilla); `loading.tsx` como `<Suspense>` de las páginas | Las lecturas a la API son dinámicas por request. Build: todas las páginas son *Partial Prerender* (esqueleto estático + datos transmitidos). |
| Errores esperados vs. inesperados | Esperados (400/404/409/422, API caída) → `ErrorAlert` en la página; inesperados → `error.tsx` | La API caída no rompe la página: se muestra "No se pudo conectar" (`Api.Unavailable`). |
| Fechas | Se muestran en `America/El_Salvador`, explícito | El servidor de Next puede correr en UTC (Docker); sin zona fija, la hora mostrada dependería de dónde corre. |
| Estilos | Tailwind mínimo, fuente del sistema | Lo visual no se evalúa. Sin fuentes de Google: el build de Docker no depende de descargarlas. |
| 404 con streaming | La página "No encontrado" se muestra, pero con status HTTP 200 | Con Partial Prerender el esqueleto se envía antes de consultar la API; una vez enviado el primer byte, el status no se puede cambiar. Next incluye el componente not-found en el payload y lo muestra al hidratar. El 404 "real" lo da la API (que es lo que se evalúa); el frontend es la presentación. |
| `toProblem` | Lee el ProblemDetails con respaldo para cada campo | Un error de la API nunca rompe la página: si el cuerpo no es JSON (un proxy, un 502), igual se muestra un error con el status real. |

## 19. Docker (§12)

```
docker-compose.yml         db, api, frontend, seq, jaeger
.env.example               contraseña propia de SQL Server (opcional)
backend/Dockerfile         sdk:10.0 → aspnet:10.0 (usuario sin privilegios, puerto 8080)
frontend/Dockerfile        node:24-alpine, salida standalone (server.js), usuario node
```

```bash
docker-compose up --build
```

| Servicio | URL | Para qué |
|---|---|---|
| frontend | http://localhost:3000 | La aplicación |
| api | http://localhost:5000 | Postman (paso 17) |
| seq | http://localhost:5341 | Logs |
| jaeger | http://localhost:16686 | Trazas |
| db | (solo red interna) | SQL Server |

| Decisión | Elegido | Por qué |
|---|---|---|
| Servicios | **Los 5 en el compose** | Un solo `docker-compose up` muestra también la observabilidad (10% de la nota) y el opcional de Jaeger, sin pasos extra. |
| Contraseña de SQL Server | **`${MSSQL_SA_PASSWORD:-<demo>}`** + `.env.example` | Funciona tal cual con `docker-compose up` (lo que pide el §12) y se puede cambiar con un `.env` ignorado por git. El valor de demo es solo para el contenedor local. |
| Orden de arranque | `db` con healthcheck (`sqlcmd SELECT 1`) → `api` → `frontend` | La API arranca con SQL Server ya listo; además reintenta por su cuenta (paso 13), así que no depende solo del healthcheck. |
| Configuración | Variables de entorno (`ConnectionStrings__Mundialito`, `Serilog__WriteTo__Seq__...`, `OpenTelemetry__OtlpEndpoint`, `API_URL`) | El código no cambia entre local y Docker: solo la configuración. Los servicios se encuentran por nombre en la red de Docker (`db`, `seq`, `jaeger`, `api`). |
| Imágenes | Multi-stage: compilar con la imagen grande, ejecutar con la chica | La imagen final no lleva SDK, compilador ni código fuente. Restore/`npm ci` en capas separadas para aprovechar la caché. |
| Seguridad de contenedores | API y frontend corren con usuario sin privilegios (`$APP_UID`, `node`) | Buenas prácticas de contenedores: nada corre como root. |
| Frontend | `output: "standalone"` | Imagen mínima: solo `server.js` y los archivos que usa, sin el `node_modules` completo. Se eliminó `public/` (estaba vacía y git no versiona carpetas vacías: el `COPY` fallaría al clonar). |
| Datos | Volumen para SQL Server y para Seq | Los datos sobreviven a `docker-compose down` y `up`. Para empezar de cero: `docker-compose down -v` (vuelve a sembrar). Jaeger guarda en memoria (all-in-one de desarrollo). |
| Ambiente de la API | Production (default de la imagen) | Logs en JSON compacto; sin OpenAPI. Lo de desarrollo (texto legible, Debug) queda para correr local. |

**Verificado desde cero** (volúmenes nuevos): db `healthy` → api aplica 4 migraciones y siembra → frontend. La API responde en el 5000 con la tabla sembrada y el POST idempotente; el frontend muestra la tabla (hablando con la API por la red interna); Seq recibe los logs con su CorrelationId; Jaeger tiene la traza del POST con sus spans SQL.

## 20. Colección de Postman (§14)

```
postman/
  Mundialito.postman_collection.json         50 requests, 7 carpetas, 165 aserciones
  Mundialito-Docker.postman_environment.json baseUrl = http://localhost:5000
  Mundialito-Local.postman_environment.json  baseUrl = http://localhost:5092
```

| Carpeta | Qué cubre |
|---|---|
| 1. Equipos | CRUD completo: POST, GET, listado filtrado, PUT, PATCH, DELETE y DELETE repetido (idempotente) |
| 2. Jugadores | Registro por equipo, listado y detalle |
| 3. Partidos | Programar, registrar 2-1, repetir el mismo resultado, corregir a 1-1 |
| 4. Torneo | Tabla (la corrección se refleja; empate total = posición compartida; orden oficial) y goleadores |
| 5. Idempotencia | Misma key → mismo id, Location y `Idempotent-Replayed`; un solo registro; otro contenido → 422; sin key → 400 |
| 6. Filtros y paginación | Páginas, `totalPages`, orden por nombre, filtros de equipos, partidos (estado, equipo, fechas), jugadores y goleadores |
| 7. Errores | 400, 404 y 409 de negocio y de consulta, cada uno con su `code` y `traceId` |

| Decisión | Elegido | Por qué |
|---|---|---|
| Organización | **Carpetas por recurso + carpetas por caso del §14** | El flujo normal queda junto a su recurso; idempotencia, filtros/paginación y errores tienen carpeta propia: el evaluador encuentra cada requisito por nombre. |
| Tests | **En cada request** (status, `code`, contenido) + uno a nivel colección (headers `X-Trace-Id` / `X-Correlation-Id`) | Con *Run collection* (o `newman`) la colección es una suite de pruebas de la API: verde/rojo sin comparar respuestas a ojo. |
| Ids | Encadenados con variables de colección (`{{teamId}}`, `{{matchId}}`...) que guardan los tests | Los Ids los genera el dominio: no se pueden escribir fijos. La colección funciona contra cualquier base recién sembrada. |
| Repetible | `{{runTag}}` único por corrida en los nombres | Se puede ejecutar muchas veces sobre la misma base sin chocar con "nombre duplicado". |
| Idempotency-Key | `{{$guid}}` nuevo por request; la carpeta 5 reutiliza una a propósito | Muestra los tres casos del §6: repetición, key reusada y key ausente. |
| Trazabilidad | `X-Correlation-Id: postman-{{runTag}}` en todos los requests | En Seq, `CorrelationId = 'postman-...'` muestra todos los logs de una corrida (§7). |
| Variables de entorno | Dos entornos con `baseUrl` | Docker (5000) o la API local (5092), sin tocar la colección. |
| Generación | Script (no versionado) que produce el JSON | 50 requests escritos de forma uniforme; el JSON versionado es lo que se importa en Postman. |

**Verificado:** `newman` contra el docker-compose: 50 requests, 165 aserciones, 0 fallos; una segunda corrida sobre la misma base también pasa completa.

## 21. Diagrama de arquitectura y README (§13)

| Decisión | Elegido | Por qué |
|---|---|---|
| Formato | **Mermaid** dentro de Markdown | GitHub lo dibuja solo; queda versionado como texto (un cambio de arquitectura se revisa con `git diff`) y no hay imágenes que se desactualicen. Los 6 diagramas se validaron con `mermaid-cli` antes de publicarlos. |
| Vistas | **Cinco diagramas enfocados** en [ARQUITECTURA.md](ARQUITECTURA.md) + uno general en el README | Despliegue, capas, escritura, lectura y trazabilidad: cada uno responde una pregunta y entre todos cubren los 11 elementos del §13. Un único diagrama con todo sería ilegible. |
| README | Puerta de entrada: levantar con un comando, cómo probar, arquitectura y una tabla **requisito → dónde se cumple** | El evaluador encuentra cada punto de la prueba sin recorrer el código. |
| Desarrollo local | El compose expone SQL Server (14333) y OTLP (4317) | `docker-compose up -d db seq jaeger` + `dotnet run`: sin contenedores de desarrollo aparte. Los puertos coinciden con `appsettings.Development.json`. |
