using Mundialito.Application.Abstractions.Messaging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Teams;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Results;
using Mundialito.Domain.Teams;

namespace Mundialito.Application.Matches.Commands.ScheduleMatch;

internal sealed class ScheduleMatchHandler(
    ITeamRepository teams,
    IMatchRepository matches,
    IUnitOfWork unitOfWork) : ICommandHandler<ScheduleMatchCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(ScheduleMatchCommand command, CancellationToken cancellationToken)
    {
        var matchResult = Match.Schedule(command.HomeTeamId, command.AwayTeamId, command.ScheduledAtUtc);
        if (matchResult.IsFailure)
        {
            return matchResult.Error;
        }

        foreach (var teamId in (Guid[])[command.HomeTeamId, command.AwayTeamId])
        {
            if (!await teams.ExistsAsync(teamId, cancellationToken))
            {
                return TeamErrors.NotFound(teamId);
            }
        }

        var match = matchResult.Value;
        matches.Add(match);

        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            return commit.Error;
        }

        return Result.Success(match.Id);
    }
}
