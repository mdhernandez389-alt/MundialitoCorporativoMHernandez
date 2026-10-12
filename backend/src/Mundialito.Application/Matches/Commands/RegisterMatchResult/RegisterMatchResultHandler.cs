using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Players;
using Mundialito.Application.Standings;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Results;
using Mundialito.Domain.Standings;

namespace Mundialito.Application.Matches.Commands.RegisterMatchResult;

/// <summary>
/// Caso de uso central del torneo: partido + goles + dos filas de la tabla, en una sola transacción.
/// </summary>
internal sealed class RegisterMatchResultHandler(
    IMatchRepository matches,
    IPlayerRepository players,
    ITeamStandingRepository standings,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterMatchResultCommand>
{
    public async Task<Result> HandleAsync(RegisterMatchResultCommand command, CancellationToken cancellationToken)
    {
        var match = await matches.GetByIdAsync(command.MatchId, cancellationToken);
        if (match is null)
        {
            return MatchErrors.NotFound(command.MatchId);
        }

        var scorersResult = await ResolveScorersAsync(command, cancellationToken);
        if (scorersResult.IsFailure)
        {
            return scorersResult.Error;
        }

        // Se guarda antes de registrar: con él la tabla revierte el resultado viejo si es una corrección.
        var previousScore = match.Score;

        var registration = match.RegisterResult(scorersResult.Value);
        if (registration.IsFailure)
        {
            return registration;
        }

        var home = await GetStandingAsync(match.HomeTeamId, cancellationToken);
        var away = await GetStandingAsync(match.AwayTeamId, cancellationToken);

        // Tras un RegisterResult exitoso el partido siempre tiene marcador.
        StandingsUpdater.ApplyResultChange(home, away, previousScore, match.Score!);

        // Partido + goles + 2 filas de tabla + evento MatchResultRegistered: todo o nada.
        // Si otro request tocó el partido o la tabla a la vez, la rowversion lo detecta → 409.
        return await unitOfWork.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Convierte los Ids recibidos (goles y autogoles) en goleadores con su equipo, trayendo todos los
    /// jugadores en una sola consulta.
    /// </summary>
    private async Task<Result<List<GoalScorer>>> ResolveScorersAsync(
        RegisterMatchResultCommand command,
        CancellationToken cancellationToken)
    {
        var distinctIds = command.ScorerPlayerIds.Concat(command.OwnGoalPlayerIds).Distinct().ToList();
        var found = await players.GetByIdsAsync(distinctIds, cancellationToken);
        var teamByPlayer = found.ToDictionary(p => p.Id, p => p.TeamId);

        foreach (var playerId in distinctIds)
        {
            if (!teamByPlayer.ContainsKey(playerId))
            {
                return PlayerErrors.NotFound(playerId);
            }
        }

        // Se recorren las listas originales (no la distinta): cada aparición es un gol.
        // El equipo es siempre el del jugador; a quién suma un autogol lo decide Match.
        var goals = command.ScorerPlayerIds.Select(id => new GoalScorer(id, teamByPlayer[id]));
        var ownGoals = command.OwnGoalPlayerIds.Select(id => new GoalScorer(id, teamByPlayer[id], IsOwnGoal: true));

        return Result.Success(goals.Concat(ownGoals).ToList());
    }

    private async Task<TeamStanding> GetStandingAsync(Guid teamId, CancellationToken cancellationToken) =>
        await standings.GetByTeamIdAsync(teamId, cancellationToken)
        // Cada equipo nace con su fila (CreateTeam). Que falte es un dato corrupto, no un caso de negocio.
        ?? throw new InvalidOperationException($"El equipo '{teamId}' no tiene fila en la tabla de posiciones.");
}
