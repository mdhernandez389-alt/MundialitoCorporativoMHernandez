using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Teams.Commands.UpdateTeam;

internal sealed class UpdateTeamHandler(ITeamRepository teams, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateTeamCommand>
{
    public async Task<Result> HandleAsync(UpdateTeamCommand command, CancellationToken cancellationToken)
    {
        var team = await teams.GetByIdAsync(command.TeamId, cancellationToken);
        if (team is null)
        {
            return TeamErrors.NotFound(command.TeamId);
        }

        var update = team.Update(command.Name, command.Department);
        if (update.IsFailure)
        {
            return update;
        }

        // Excluye al propio equipo: un PUT que conserva su nombre no es un duplicado.
        if (await teams.NameExistsAsync(team.Name, team.Id, cancellationToken))
        {
            return TeamErrors.NameDuplicated;
        }

        return await unitOfWork.CommitAsync(cancellationToken);
    }
}
