using Mundialito.Domain.Results;

namespace Mundialito.Domain.Teams;

public static class TeamErrors
{
    public static readonly Error NameRequired =
        Error.Validation("Team.NameRequired", "El nombre del equipo es obligatorio.");

    public static readonly Error NameTooLong =
        Error.Validation("Team.NameTooLong", $"El nombre del equipo no puede superar {Team.NameMaxLength} caracteres.");

    public static readonly Error DepartmentRequired =
        Error.Validation("Team.DepartmentRequired", "El departamento del equipo es obligatorio.");

    public static readonly Error DepartmentTooLong =
        Error.Validation("Team.DepartmentTooLong", $"El departamento no puede superar {Team.DepartmentMaxLength} caracteres.");

    public static readonly Error NameDuplicated =
        Error.Conflict("Team.NameDuplicated", "Ya existe un equipo con ese nombre.");

    public static readonly Error HasMatches =
        Error.Conflict("Team.HasMatches", "No se puede eliminar un equipo que tiene partidos: dejaría inconsistente la tabla de posiciones.");

    public static Error NotFound(Guid teamId) =>
        Error.NotFound("Team.NotFound", $"No existe el equipo '{teamId}'.");
}
