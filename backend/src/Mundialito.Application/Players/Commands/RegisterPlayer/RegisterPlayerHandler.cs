using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Teams;
using Mundialito.Domain.Players;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Players.Commands.RegisterPlayer;

internal sealed class RegisterPlayerHandler(
    ITeamRepository teams,
    IPlayerRepository players,
    IUnitOfWork unitOfWork) : ICommandHandler<RegisterPlayerCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(RegisterPlayerCommand command, CancellationToken cancellationToken)
    {
        // El equipo va en la ruta (/teams/{teamId}/players): si no existe, el recurso padre no existe → 404 primero.
        if (!await teams.ExistsAsync(command.TeamId, cancellationToken))
        {
            return TeamErrors.NotFound(command.TeamId);
        }

        var playerResult = Player.Create(command.TeamId, command.FullName, command.ShirtNumber);
        if (playerResult.IsFailure)
        {
            return playerResult.Error;
        }

        // El índice único (TeamId, ShirtNumber) cubre la carrera entre dos requests.
        if (await players.ShirtNumberTakenAsync(command.TeamId, command.ShirtNumber, cancellationToken))
        {
            return PlayerErrors.ShirtNumberTaken;
        }

        var player = playerResult.Value;
        players.Add(player);

        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            return commit.Error;
        }

        return Result.Success(player.Id);
    }
}
