# Arquitectura — Mundialito Corporativo

Cinco vistas, cada una responde una pregunta. Entre todas cubren lo que pide el §13: capas, CQRS,
EF Core (escritura), Dapper (lectura), Unit of Work, almacenamiento de idempotencia, eventos, logs,
flujo de traza, frontend y Docker. El *porqué* de cada decisión está en [DECISIONES.md](DECISIONES.md).

| Vista | Pregunta |
|---|---|
| [1. Despliegue](#1-despliegue-docker) | ¿Qué corre y cómo se conecta? |
| [2. Capas](#2-capas-clean-architecture) | ¿Quién depende de quién? |
| [3. Escritura](#3-escritura-command-ef-core--unit-of-work) | ¿Qué pasa en un POST/PUT? (idempotencia, UoW, EF Core, eventos) |
| [4. Lectura](#4-lectura-query-dapper) | ¿Qué pasa en un GET? (Dapper, paginación en la base) |
| [5. Trazabilidad](#5-trazabilidad-logs-trazas-y-eventos) | ¿Cómo se sigue un request? (CorrelationId, TraceId, Seq, Jaeger) |

---

## 1. Despliegue (Docker)

Un `docker-compose up` levanta los cinco servicios. Dentro de la red de Docker se encuentran por nombre.

```mermaid
flowchart LR
    browser(["Navegador"])
    postman(["Postman / newman"])

    subgraph compose["docker-compose up"]
        fe["frontend<br/>Next.js 16<br/>:3000"]
        api["api<br/>ASP.NET Core 10<br/>:5000 → 8080"]
        db[("db<br/>SQL Server 2022")]
        seq["seq<br/>logs<br/>:5341"]
        jaeger["jaeger<br/>trazas<br/>:16686"]
    end

    browser -->|"HTTP :3000"| fe
    postman -->|"HTTP :5000"| api
    fe -->|"http://api:8080<br/>red interna, desde el servidor de Next"| api
    api -->|"EF Core escribe<br/>Dapper lee"| db
    api -->|"logs Serilog"| seq
    api -->|"trazas OTLP :4317"| jaeger
```

- El **navegador nunca habla con la API**: el frontend la llama desde el servidor de Next (Server Components
  y Server Actions). Sin CORS y sin exponer la URL de la API al navegador.
- La API **migra y siembra la base sola** al arrancar (con reintentos mientras SQL Server se inicia).

---

## 2. Capas (Clean Architecture)

Las flechas son referencias entre proyectos: el compilador impide cualquier otra dependencia.

```mermaid
flowchart TB
    subgraph api["Mundialito.Api — solo orquesta"]
        ctrl["Controllers<br/>Result → HTTP · ProblemDetails + code"]
        pipe["CorrelationIdMiddleware · log por request<br/>Filtro [Idempotent]"]
    end

    subgraph infra["Mundialito.Infrastructure — implementa los contratos"]
        write["Escritura: DbContext EF Core · UnitOfWork · Repositorios"]
        read["Lectura: Query handlers con Dapper"]
        idem["SqlIdempotencyStore"]
        seed["DatabaseInitializer · DataSeeder"]
    end

    subgraph app["Mundialito.Application — casos de uso"]
        cmds["Commands + handlers"]
        qrys["Queries + DTOs"]
        ports["Contratos: IUnitOfWork · repositorios<br/>IIdempotencyStore · ICorrelationContext"]
    end

    subgraph dom["Mundialito.Domain — sin dependencias"]
        ents["Team · Player · Match · TeamStanding<br/>StandingsUpdater"]
        res["Result · Error"]
        evts["TeamCreated · MatchResultRegistered"]
    end

    api --> app
    api -->|"solo para armar la DI"| infra
    infra --> app
    app --> dom
```

| Capa | Depende de | Contiene |
|---|---|---|
| Domain | nada | Reglas del negocio: entidades que se validan solas, `Result`, eventos, la regla de la tabla |
| Application | Domain | Casos de uso (Commands y Queries) y los **contratos** que necesita (sin EF ni Dapper) |
| Infrastructure | Application | Implementaciones: EF Core + UnitOfWork para escribir, Dapper para leer, idempotencia, seed |
| Api | Application + Infrastructure | Controllers que traducen HTTP ↔ Commands/Queries y `Result` → status code |

---

## 3. Escritura (Command, EF Core + Unit of Work)

Ejemplo: `POST /api/teams`. Todo Command termina en **un** `CommitAsync`: una transacción con los
cambios de todos los repositorios y sus eventos de dominio.

```mermaid
sequenceDiagram
    autonumber
    participant C as Cliente
    participant F as Filtro Idempotent
    participant S as IdempotencyStore (SQL)
    participant H as CreateTeamHandler
    participant D as Dominio
    participant U as UnitOfWork (EF Core)
    participant DB as SQL Server

    C->>F: POST /api/teams + Idempotency-Key
    F->>S: reservar la key (INSERT atómico con bloqueo)
    alt key ya usada con el mismo contenido
        S-->>F: respuesta guardada
        F-->>C: la misma respuesta + Idempotent-Replayed
    else key nueva
        F->>H: ejecutar la acción
        H->>D: Team.Create() devuelve Result
        D-->>H: equipo válido + evento TeamCreated
        H->>U: repositorios Add() y CommitAsync()
        U->>DB: BEGIN · INSERT Teams, TeamStandings, DomainEvents · COMMIT
        U-->>H: Result Success o Conflict (duplicado, concurrencia)
        H-->>F: 201 Created o ProblemDetails
        F->>S: guardar la respuesta si es 2xx, si no liberar la key
        F-->>C: 201 Created + Location
    end
```

- **Unit of Work:** único punto que guarda (`SaveChanges` bloqueado fuera de él). Traduce los rechazos de la
  base (índice único, `rowversion`) a `Result` con 409, sin excepciones como control de flujo.
- **Eventos de dominio:** se guardan en la tabla `DomainEvents` en la misma transacción y se loggean después
  del commit.
- **Idempotencia:** misma key y mismo contenido → misma respuesta; otro contenido → 422; en curso → 409.

---

## 4. Lectura (Query, Dapper)

Ejemplo: `GET /api/standings`. Exclusivamente Dapper: SQL directo a DTOs, paginado en la base.

```mermaid
sequenceDiagram
    participant C as Cliente
    participant Ctl as StandingsController
    participant Q as GetStandingsHandler (Dapper)
    participant DB as SQL Server

    C->>Ctl: GET /api/standings?pageNumber=1 y pageSize=10 y sortBy=points
    Ctl->>Q: GetStandingsQuery
    Q->>Q: valida pageSize (1 a 100) y sortBy contra la lista blanca
    Q->>DB: COUNT + página con OFFSET/FETCH (dos SELECT, un solo viaje)
    DB-->>Q: total + filas de la página
    Q-->>Ctl: Result con la página
    Ctl-->>C: 200 data, pageNumber, pageSize, totalRecords, totalPages
```

- **Nunca se trae todo a memoria:** viajan como máximo 100 filas por consulta.
- **`sortBy` seguro:** el texto del usuario solo se busca en un diccionario; al SQL llega la columna escrita
  por nosotros (sin SQL Injection), con el Id como desempate para que las páginas sean estables.
- **Tabla de posiciones:** se lee de `TeamStandings` (ya calculada en cada resultado) con `RANK()` sobre
  puntos → diferencia de gol → goles a favor.

---

## 5. Trazabilidad (logs, trazas y eventos)

Un mismo **TraceId** une la respuesta, los logs, la traza y los eventos guardados.

```mermaid
flowchart LR
    req["Request<br/>X-Correlation-Id opcional"] --> mw["CorrelationIdMiddleware<br/>CorrelationId + TraceId W3C"]

    mw --> logs["Serilog<br/>cada log con TraceId y CorrelationId<br/>una línea por request con su duración"]
    mw --> otel["OpenTelemetry<br/>span del request + un span por consulta SQL"]
    mw --> uow["UnitOfWork<br/>evento guardado con TraceId y CorrelationId"]
    mw --> resp["Respuesta<br/>X-Trace-Id · X-Correlation-Id<br/>traceId en los ProblemDetails"]

    logs --> seq[("Seq")]
    otel --> jaeger[("Jaeger")]
    uow --> events[("SQL Server<br/>tabla DomainEvents")]
```

Para seguir un request: copiar el `traceId` de la respuesta (o de un error) → buscarlo en **Seq**
(`@TraceId = '...'`) para ver sus logs → abrirlo en **Jaeger** para ver cada paso con su duración →
consultarlo en `DomainEvents` para ver los eventos que generó.
