using Mundialito.Domain.Matches;
using Mundialito.Domain.Results;

namespace Mundialito.Application.Matches.Queries.GetMatches;

public static class MatchQueryErrors
{
    public static readonly Error InvalidStatus =
        Error.Validation(
            "Match.InvalidStatusFilter",
            $"status debe ser uno de: {string.Join(", ", Enum.GetNames<MatchStatus>())}.");

    public static readonly Error InvalidDateRange =
        Error.Validation("Match.InvalidDateRange", "from no puede ser posterior a to.");
}
