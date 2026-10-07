using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Matches.Commands.ScheduleMatch;

/// <summary>POST /matches. Devuelve el Id del partido programado.</summary>
public sealed record ScheduleMatchCommand(Guid HomeTeamId, Guid AwayTeamId, DateTime ScheduledAtUtc) : ICommand<Guid>;
