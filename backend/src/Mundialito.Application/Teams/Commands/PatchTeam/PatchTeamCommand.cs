using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.PatchTeam;

/// <summary>
/// PATCH /teams/{id}: modificación parcial. Un campo <c>null</c> (o ausente en el JSON) conserva su valor
/// actual, como en JSON Merge Patch (RFC 7396).
/// </summary>
public sealed record PatchTeamCommand(Guid TeamId, string? Name, string? Department) : ICommand;
