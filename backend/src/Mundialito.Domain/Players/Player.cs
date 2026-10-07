using Mundialito.Domain.Abstractions;
using Mundialito.Domain.Results;

namespace Mundialito.Domain.Players;

/// <summary>
/// Integrante de un equipo. Se relaciona con <c>Team</c> solo por su Id: son agregados distintos,
/// así se pueden paginar y filtrar jugadores sin cargar el equipo completo.
/// </summary>
public sealed class Player : Entity
{
    public const int FullNameMaxLength = 100;
    public const int MinShirtNumber = 1;
    public const int MaxShirtNumber = 99;

    private Player(Guid id, Guid teamId, string fullName, int shirtNumber)
        : base(id)
    {
        TeamId = teamId;
        FullName = fullName;
        ShirtNumber = shirtNumber;
    }

    public Guid TeamId { get; }

    public string FullName { get; private set; }

    public int ShirtNumber { get; private set; }

    /// <summary>
    /// Registra un jugador en un equipo. Que el equipo exista y el dorsal esté libre lo valida el handler.
    /// </summary>
    public static Result<Player> Create(Guid teamId, string fullName, int shirtNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return PlayerErrors.FullNameRequired;
        }

        if (fullName.Trim().Length > FullNameMaxLength)
        {
            return PlayerErrors.FullNameTooLong;
        }

        if (shirtNumber is < MinShirtNumber or > MaxShirtNumber)
        {
            return PlayerErrors.InvalidShirtNumber;
        }

        return Result.Success(new Player(Guid.CreateVersion7(), teamId, fullName.Trim(), shirtNumber));
    }
}
