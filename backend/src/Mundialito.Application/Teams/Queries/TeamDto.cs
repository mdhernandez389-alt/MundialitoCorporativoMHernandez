namespace Mundialito.Application.Teams.Queries;

/// <summary>Equipo tal como lo lee el lado Query: datos planos, sin reglas ni eventos.</summary>
public sealed record TeamDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Department { get; init; } = string.Empty;
}
