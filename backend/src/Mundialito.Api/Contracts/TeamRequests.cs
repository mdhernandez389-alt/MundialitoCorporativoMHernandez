namespace Mundialito.Api.Contracts;

// Los textos no llevan [Required]: un nombre ausente llega como null y lo rechaza el dominio
// con su propio código (Team.NameRequired). Una sola fuente de validación.

public sealed record CreateTeamRequest(string Name, string Department);

public sealed record UpdateTeamRequest(string Name, string Department);

/// <summary>Campo ausente o <c>null</c> = no cambiar (JSON Merge Patch).</summary>
public sealed record PatchTeamRequest(string? Name, string? Department);
