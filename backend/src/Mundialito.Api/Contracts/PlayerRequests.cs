namespace Mundialito.Api.Contracts;

/// <summary>Un dorsal ausente llega como 0 y el dominio lo rechaza (Player.InvalidShirtNumber).</summary>
public sealed record RegisterPlayerRequest(string FullName, int ShirtNumber);
