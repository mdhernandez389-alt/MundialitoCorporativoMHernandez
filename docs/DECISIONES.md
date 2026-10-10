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
