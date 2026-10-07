using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.UpdateTeam;

/// <summary>PUT /teams/{id}: reemplazo completo. Repetirlo con los mismos datos no cambia nada (idempotente).</summary>
public sealed record UpdateTeamCommand(Guid TeamId, string Name, string Department) : ICommand;
