using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.DeleteTeam;

internal sealed class DeleteTeamHandler(
    ITeamRepository teams,
    IMatchRepository matches,
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteTeamCommand>
{
    public async Task<Result> HandleAsync(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teams.GetByIdAsync(command.TeamId, cancellationToken);
        // DELETE idempotente (§6): si no existe, el estado pedido ("ese equipo no existe") ya se cumple.
        // Repetir el DELETE responde lo mismo que la primera vez.
        if (team is null)
        {
            return Result.Success();
        }

        // Sus rivales tienen puntos ganados contra él: borrarlo dejaría la tabla inconsistente.
        // Si alguien programa un partido justo ahora, la FK lo frena y el UnitOfWork devuelve 409.
        if (await matches.ExistsForTeamAsync(team.Id, cancellationToken))
        {
            return TeamErrors.HasMatches;
        }

        // Jugadores y fila de la tabla se borran en cascada (sin partidos no hay goles que proteger).
        teams.Remove(team);

        return await unitOfWork.CommitAsync(cancellationToken);
    }
}
