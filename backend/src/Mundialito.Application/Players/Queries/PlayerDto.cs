namespace Mundialito.Application.Players.Queries;

public sealed record PlayerDto
{
    public Guid Id { get; init; }

    public Guid TeamId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public int ShirtNumber { get; init; }
}
