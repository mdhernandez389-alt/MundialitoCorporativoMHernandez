# Mundialito Corporativo — Prueba técnica INFATLAN SV

## Regla principal

Todo el proyecto se rige por el documento de la prueba: [docs/Gmail - Prueba Técnica INFATLAN SV.html](docs/Gmail%20-%20Prueba%20Técnica%20INFATLAN%20SV.html).
Cada decisión, archivo y paso debe cumplir sus estándares y su finalidad. Si algo no está claro en el documento,
se define un criterio propio sin perder de vista el requerimiento general, y se anota en [docs/DECISIONES.md](docs/DECISIONES.md).

Puntos obligatorios (sección del documento):

- §2 Clean Architecture: Domain (sin dependencias) ← Application (solo Domain) ← Infrastructure (implementa contratos) ← Api (solo orquesta).
- §3 CQRS: Commands con EF Core + Unit of Work + transacciones; Queries **exclusivamente** con Dapper, paginación en la base.
- §4 Unit of Work: transacciones centralizadas, commit explícito, usado en todos los Commands. Nunca `SaveChanges` fuera del UoW.
- §5 Result Pattern: `Result` / `Result<T>` con Success, Failure, mensaje y código de error opcional. **Sin excepciones como control de flujo.** La API mapea a 200/201/400/404/409.
- §6 REST completo (GET/POST/PUT/PATCH/DELETE idempotentes) + `Idempotency-Key` en POST persistida, misma respuesta ante repetición.
- §7 Observabilidad: logs estructurados (sin datos sensibles, niveles correctos), CorrelationId/TraceId, duración de requests, eventos de dominio `TeamCreated` y `MatchResultRegistered` registrados y loggeados.
- §8 Funcional: equipos (CRUD, filtros, paginación, orden dinámico), jugadores por equipo, partidos (filtros por fecha/equipo/estado), tabla (puntos → diferencia de gol → goles a favor) y goleadores.
- §9 Seed en Infrastructure si la DB está vacía: 4 equipos, 5 jugadores c/u, 6 partidos, 3 con resultado.
- §10 Todos los GET: `pageNumber`, `pageSize`, `sortBy`, `sortDirection` + filtros; respuesta `{ data, pageNumber, pageSize, totalRecords, totalPages }`. Nunca traer todo a memoria.
- §11 Frontend Next.js desacoplado (no se evalúa lo visual).
- §12 Dockerfile API + Dockerfile Frontend + `docker-compose.yml` (API, DB, Frontend) con `docker-compose up`.
- §13 Diagrama de arquitectura · §14 Colección Postman · §15 Pruebas unitarias.

## Forma de trabajo

- Se avanza según [ROADMAP.md](ROADMAP.md), un paso a la vez, explicando cada decisión antes de seguir.
- Las decisiones tomadas y su justificación van en [docs/DECISIONES.md](docs/DECISIONES.md) (sirven para defender la solución en la entrevista).
- Stack: .NET 10, SQL Server, Controllers, handlers CQRS propios (sin MediatR).
