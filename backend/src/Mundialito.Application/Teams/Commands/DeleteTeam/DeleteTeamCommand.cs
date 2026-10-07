using Mundialito.Application.Abstractions.Messaging;

namespace Mundialito.Application.Teams.Commands.DeleteTeam;

/// <summary>DELETE /teams/{id}. Solo si el equipo no tiene partidos; sus jugadores y su fila de tabla se van con él.</summary>
public sealed record DeleteTeamCommand(Guid TeamId) : ICommand;
