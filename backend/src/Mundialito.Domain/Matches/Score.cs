namespace Mundialito.Domain.Matches;

/// <summary>
/// Marcador final (value object). Nunca se recibe del cliente: <see cref="Match"/> lo deriva de los goles.
/// </summary>
public sealed record Score(int HomeGoals, int AwayGoals);
