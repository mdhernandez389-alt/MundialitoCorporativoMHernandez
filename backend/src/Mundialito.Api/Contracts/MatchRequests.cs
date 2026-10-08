using System.ComponentModel.DataAnnotations;

namespace Mundialito.Api.Contracts;

// Aquí sí hay [Required]: un Guid o una fecha ausentes llegarían como Guid.Empty o 0001-01-01,
// valores "válidos" que el dominio no puede distinguir de un dato real.

/// <param name="ScheduledAt">Con zona horaria (p. ej. <c>2026-10-10T18:00:00-06:00</c>); se guarda en UTC.</param>
public sealed record ScheduleMatchRequest(
    [Required] Guid? HomeTeamId,
    [Required] Guid? AwayTeamId,
    [Required] DateTimeOffset? ScheduledAt);

/// <param name="ScorerPlayerIds">
/// Un Id por gol (un doblete = dos veces el mismo Id). Obligatorio: <c>[]</c> registra un 0 a 0 a propósito;
/// olvidarse del campo no debe registrarlo por accidente.
/// </param>
public sealed record RegisterMatchResultRequest([Required] IReadOnlyCollection<Guid>? ScorerPlayerIds);
