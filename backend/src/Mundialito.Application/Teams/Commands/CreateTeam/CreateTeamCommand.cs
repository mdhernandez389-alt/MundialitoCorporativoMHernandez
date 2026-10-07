using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.CreateTeam;

/// <summary>POST /teams. Devuelve el Id del equipo creado.</summary>
public sealed record CreateTeamCommand(string Name, string Department) : ICommand<Guid>;
