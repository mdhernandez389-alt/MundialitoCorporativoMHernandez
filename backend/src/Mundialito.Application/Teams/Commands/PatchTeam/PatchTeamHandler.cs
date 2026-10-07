using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.PatchTeam;

internal sealed class PatchTeamHandler(ITeamRepository teams, IUnitOfWork unitOfWork)
    : ICommandHandler<PatchTeamCommand>
{
    public async Task<Result> HandleAsync(PatchTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teams.GetByIdAsync(command.TeamId, cancellationToken);
        if (team is null)
        {
            return TeamErrors.NotFound(command.TeamId);
        }

        // Completa lo que no vino con el valor actual y pasa por la misma regla que el PUT.
        var update = team.Update(command.Name ?? team.Name, command.Department ?? team.Department);
        if (update.IsFailure)
        {
            return update;
        }

        if (await teams.NameExistsAsync(team.Name, team.Id, cancellationToken))
        {
            return TeamErrors.NameDuplicated;
        }

        return await unitOfWork.CommitAsync(cancellationToken);
    }
}
