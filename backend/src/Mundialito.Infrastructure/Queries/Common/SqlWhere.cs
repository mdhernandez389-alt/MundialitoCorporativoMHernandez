using Dapper;

namespace Mundialito.Infrastructure.Queries.Common;

/// <summary>
/// Arma el WHERE solo con los filtros que llegaron. Las condiciones son texto fijo escrito por nosotros;
/// lo que manda el usuario va siempre como parámetro (<c>@Name</c>), nunca concatenado.
/// </summary>
/// <remarks>
/// Agregar solo los filtros presentes (en vez de <c>(@Name IS NULL OR ...)</c>) le permite a SQL Server
/// armar un plan de ejecución a la medida de cada combinación de filtros.
/// </remarks>
internal sealed class SqlWhere
{
    // ESCAPE: así un "%" o "_" escrito por el usuario se busca literal, no como comodín.
    private const string LikeEscape = "ESCAPE '\\'";

    private readonly List<string> _conditions = [];

    public DynamicParameters Parameters { get; } = new();

    public SqlWhere Add(string condition, string parameterName, object value)
    {
        _conditions.Add(condition);
        Parameters.Add(parameterName, value);
        return this;
    }

    /// <summary>Filtro "contiene" sin distinguir mayúsculas (collation CI). Se omite si viene vacío.</summary>
    public SqlWhere AddContains(string column, string parameterName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return this;
        }

        var escaped = value.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");

        return Add($"{column} LIKE @{parameterName} {LikeEscape}", parameterName, $"%{escaped}%");
    }

    public override string ToString() =>
        _conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", _conditions);
}
