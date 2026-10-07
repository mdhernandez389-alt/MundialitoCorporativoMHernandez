using Mundialito.Domain.Results;

namespace Mundialito.Domain.Players;

public static class PlayerErrors
{
    public static readonly Error FullNameRequired =
        Error.Validation("Player.FullNameRequired", "El nombre del jugador es obligatorio.");

    public static readonly Error FullNameTooLong =
        Error.Validation("Player.FullNameTooLong", $"El nombre del jugador no puede superar {Player.FullNameMaxLength} caracteres.");

    public static readonly Error InvalidShirtNumber =
        Error.Validation(
            "Player.InvalidShirtNumber",
            $"El dorsal debe estar entre {Player.MinShirtNumber} y {Player.MaxShirtNumber}.");

    public static readonly Error ShirtNumberTaken =
        Error.Conflict("Player.ShirtNumberTaken", "Ese dorsal ya está ocupado en el equipo.");

    public static Error NotFound(Guid playerId) =>
        Error.NotFound("Player.NotFound", $"No existe el jugador '{playerId}'.");
}
