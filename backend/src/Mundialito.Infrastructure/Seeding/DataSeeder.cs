using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mundialito.Application.Abstractions.Persistence;
using Mundialito.Application.Matches;
using Mundialito.Application.Players;
using Mundialito.Application.Standings;
using Mundialito.Application.Teams;
using Mundialito.Domain.Matches;
using Mundialito.Domain.Players;
using Mundialito.Domain.Standings;
using Mundialito.Domain.Teams;
using Mundialito.Infrastructure.Persistence;

namespace Mundialito.Infrastructure.Seeding;

/// <summary>
/// Datos de ejemplo (§9): 4 equipos, 5 jugadores por equipo, 6 partidos (todos contra todos), 3 con resultado.
/// </summary>
/// <remarks>
/// Usa el mismo camino que la API: fábricas del dominio, <see cref="StandingsUpdater"/>, repositorios y
/// un único <see cref="IUnitOfWork.CommitAsync"/>. Los datos no pueden ser inválidos, la tabla se calcula con la
/// regla real y quedan registrados los eventos TeamCreated y MatchResultRegistered.
/// </remarks>
internal sealed class DataSeeder(
    MundialitoDbContext dbContext,
    ITeamRepository teams,
    ITeamStandingRepository standings,
    IPlayerRepository players,
    IMatchRepository matches,
    IUnitOfWork unitOfWork,
    ILogger<DataSeeder> logger)
{
    // El primero de cada plantel es el 9, después el 10 y el 8: los goles se reparten entre esos tres.
    private static readonly int[] ShirtNumbers = [9, 10, 8, 5, 1];
    private const int Strikers = 3;

    private static readonly SeedTeam[] SeedTeams =
    [
        new("Sistemas", "Tecnología de la Información",
            ["Carlos Méndez", "Diego Rivas", "Andrés Molina", "Javier Castro", "Luis Herrera"]),
        new("Finanzas", "Finanzas y Contabilidad",
            ["Fernando Ruiz", "Ricardo Peña", "Sergio Campos", "Mauricio Vega", "Óscar Lima"]),
        new("Operaciones", "Operaciones Bancarias",
            ["Roberto Flores", "Eduardo Navarro", "Pablo Ortiz", "Héctor Rojas", "Manuel Cruz"]),
        new("Talento Humano", "Recursos Humanos",
            ["Alejandro Torres", "Daniel Morales", "Gabriel Reyes", "Hugo Salazar", "Iván Paredes"]),
    ];

    // Todos contra todos (6 partidos). Fechas relativas al seed: los jugados en el pasado, el resto en el futuro,
    // así siempre hay partidos pasados y próximos para los filtros por fecha, sin importar cuándo se pruebe.
    // Tabla resultante: Sistemas 6 pts, Operaciones 3, y Talento Humano y Finanzas empatados a 0 puntos,
    // separados por diferencia de gol (−1 contra −2): el 2º criterio del §8 se ve apenas se levanta el proyecto.
    private static readonly Fixture[] Fixtures =
    [
        new("Sistemas", "Finanzas", DaysFromSeed: -14, Result: new(3, 1)),
        new("Operaciones", "Talento Humano", DaysFromSeed: -7, Result: new(4, 3)),
        new("Sistemas", "Operaciones", DaysFromSeed: -3, Result: new(6, 2)),

        new("Finanzas", "Talento Humano", DaysFromSeed: 3, Result: null),
        new("Talento Humano", "Sistemas", DaysFromSeed: 7, Result: null),
        new("Finanzas", "Operaciones", DaysFromSeed: 14, Result: null),
    ];

    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        // "Vacía" = sin equipos: sin equipos no puede haber jugadores, partidos ni tabla.
        if (await dbContext.Teams.AnyAsync(cancellationToken))
        {
            logger.LogInformation("La base ya tiene datos: no se siembra");
            return;
        }

        logger.LogInformation("Base vacía: sembrando datos de ejemplo");

        var squads = SeedTeams.ToDictionary(seed => seed.Name, CreateSquad);

        // Partidos a las 18:00 UTC, contados desde el día del seed.
        var seedDay = DateTime.UtcNow.Date.AddHours(18);
        foreach (var fixture in Fixtures)
        {
            ScheduleAndPlay(fixture, squads[fixture.Home], squads[fixture.Away], seedDay);
        }

        // Todo o nada: si algo falla, no queda una base "a medias" que el próximo arranque daría por sembrada.
        var commit = await unitOfWork.CommitAsync(cancellationToken);
        if (commit.IsFailure)
        {
            // Datos fijos escritos por nosotros: que el commit falle es un bug, no un caso de negocio.
            throw new InvalidOperationException($"No se pudo sembrar la base: {commit.Error.Code}");
        }

        logger.LogInformation(
            "Seed completado: {TeamCount} equipos, {PlayerCount} jugadores, {MatchCount} partidos ({PlayedCount} con resultado)",
            SeedTeams.Length,
            SeedTeams.Length * ShirtNumbers.Length,
            Fixtures.Length,
            Fixtures.Count(f => f.Result is not null));
    }

    private Squad CreateSquad(SeedTeam seed)
    {
        // .Value: si una fábrica rechazara estos datos fijos sería un bug, y debe fallar ruidosamente.
        var team = Team.Create(seed.Name, seed.Department).Value;
        teams.Add(team);

        var standing = TeamStanding.Create(team.Id);
        standings.Add(standing);

        var squad = seed.PlayerNames
            .Select((fullName, index) => Player.Create(team.Id, fullName, ShirtNumbers[index]).Value)
            .ToList();
        squad.ForEach(players.Add);

        return new Squad(team, standing, squad);
    }

    private void ScheduleAndPlay(Fixture fixture, Squad home, Squad away, DateTime seedDay)
    {
        var match = Match.Schedule(home.Team.Id, away.Team.Id, seedDay.AddDays(fixture.DaysFromSeed)).Value;
        matches.Add(match);

        if (fixture.Result is not { } score)
        {
            return;
        }

        var scorers = home.Scorers(score.HomeGoals).Concat(away.Scorers(score.AwayGoals)).ToList();

        var registration = match.RegisterResult(scorers);
        if (registration.IsFailure)
        {
            throw new InvalidOperationException($"Resultado de seed inválido: {registration.Error.Code}");
        }

        StandingsUpdater.ApplyResultChange(home.Standing, away.Standing, previousScore: null, match.Score!);
    }

    private sealed record SeedTeam(string Name, string Department, string[] PlayerNames);

    private sealed record Fixture(string Home, string Away, int DaysFromSeed, Score? Result);

    private sealed record Squad(Team Team, TeamStanding Standing, List<Player> Players)
    {
        /// <summary>Reparte los goles entre los delanteros (9, 10, 8, 9, 10...): la tabla de goleadores tiene variedad.</summary>
        public IEnumerable<GoalScorer> Scorers(int goals) =>
            Enumerable.Range(0, goals).Select(goal => new GoalScorer(Players[goal % Strikers].Id, Team.Id));
    }
}
