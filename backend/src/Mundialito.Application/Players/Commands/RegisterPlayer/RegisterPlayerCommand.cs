using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Players.Commands.RegisterPlayer;

/// <summary>POST /teams/{teamId}/players. Devuelve el Id del jugador registrado.</summary>
public sealed record RegisterPlayerCommand(Guid TeamId, string FullName, int ShirtNumber) : ICommand<Guid>;
