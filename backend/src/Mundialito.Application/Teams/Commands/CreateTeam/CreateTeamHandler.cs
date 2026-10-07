using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Standings;
using Mundialito.Domain.Results;
using Mundialito.Domain.Standings;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.CreateTeam;

internal sealed class CreateTeamHandler(
    ITeamRepository teams,
    ITeamStandingRepository standings,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateTeamCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateTeamCommand command, CancellationToken cancellationToken)
    {
        // Primero lo que la entidad valida sola (400), después lo que requiere la base (409).
        var teamResult = Team.Create(command.Name, command.Department);
        if (teamResult.IsFailure)
        {
            return teamResult.Error;
        }

        var team = teamResult.Value;

        if (await teams.NameExistsAsync(team.Name, excludingTeamId: null, cancellationToken))
        {
            return TeamErrors.NameDuplicated;
        }

        teams.Add(team);
        // El equipo aparece en la tabla de posiciones desde que se crea, en cero.
        standings.Add(TeamStanding.Create(team.Id));

        // Equipo + fila de tabla + evento TeamCreated: todo en la misma transacción.
        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            return commit.Error;
        }

        return Result.Success(team.Id);
    }
}
