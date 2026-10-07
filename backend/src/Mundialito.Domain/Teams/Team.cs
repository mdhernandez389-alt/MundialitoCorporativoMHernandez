using Mundialito.Domain.Abstractions;
using Mundialito.Domain.Results;

namespace Mundialito.Domain.Teams;

/// <summary>
/// Equipo participante, identificado por el departamento de la empresa al que representa.
/// </summary>
/// <remarks>
/// La unicidad del nombre no se valida aquí: requiere consultar la base, así que la revisa el handler
/// (y un índice único la garantiza).
/// </remarks>
public sealed class Team : Entity
{
    public const int NameMaxLength = 100;
    public const int DepartmentMaxLength = 100;

    private Team(Guid id, string name, string department)
        : base(id)
    {
        Name = name;
        Department = department;
    }

    public string Name { get; private set; }

    public string Department { get; private set; }

    public static Result<Team> Create(string name, string department)
    {
        var validation = Validate(name, department);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var team = new Team(Guid.CreateVersion7(), name.Trim(), department.Trim());
        team.Raise(new TeamCreatedDomainEvent(team.Id, team.Name));

        return Result.Success(team);
    }

    /// <summary>Reemplaza los datos del equipo (PUT; PATCH completa los campos faltantes antes de llamarlo).</summary>
    public Result Update(string name, string department)
    {
        var validation = Validate(name, department);
        if (validation.IsFailure)
        {
            return validation;
        }

        Name = name.Trim();
        Department = department.Trim();

        return Result.Success();
    }

    private static Result Validate(string name, string department)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return TeamErrors.NameRequired;
        }

        if (name.Trim().Length > NameMaxLength)
        {
            return TeamErrors.NameTooLong;
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            return TeamErrors.DepartmentRequired;
        }

        if (department.Trim().Length > DepartmentMaxLength)
        {
            return TeamErrors.DepartmentTooLong;
        }

        return Result.Success();
    }
}
