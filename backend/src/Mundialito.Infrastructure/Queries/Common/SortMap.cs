using Mundialito.Application.Abstractions.Querying;
using Mundialito.Domain.Results;

namespace Mundialito.Infrastructure.Queries.Common;

/// <summary>
/// Lista blanca de columnas por las que se puede ordenar un listado. Es la defensa contra SQL Injection
/// en el ORDER BY: el texto del usuario solo se usa para BUSCAR aquí; lo que llega al SQL es siempre
/// un valor de este diccionario, escrito por nosotros.
/// </summary>
internal sealed class SortMap
{
    private readonly Dictionary<string, string> _columns;
    private readonly string _defaultSortBy;
    private readonly string _tieBreaker;

    /// <param name="defaultSortBy">Nombre público que se usa si el cliente no manda <c>sortBy</c>.</param>
    /// <param name="tieBreaker">Columna única (el Id) que desempata: sin ella, filas con el mismo valor
    /// pueden cambiar de orden entre consultas y aparecer en dos páginas o en ninguna.</param>
    /// <param name="columns">Nombre público (<c>name</c>) → columna SQL (<c>t.Name</c>).</param>
    public SortMap(string defaultSortBy, string tieBreaker, IReadOnlyDictionary<string, string> columns)
    {
        // "Name", "name" y "NAME" son el mismo campo para el cliente.
        _columns = new Dictionary<string, string>(columns, StringComparer.OrdinalIgnoreCase);
        _defaultSortBy = defaultSortBy;
        _tieBreaker = tieBreaker;
    }

    /// <summary>
    /// Devuelve el contenido del ORDER BY (p. ej. <c>t.Name DESC, t.Id</c>) o un error 400 si
    /// <c>sortBy</c> o <c>sortDirection</c> no son válidos.
    /// </summary>
    public Result<string> Resolve(string? sortBy, string? sortDirection)
    {
        var key = string.IsNullOrWhiteSpace(sortBy) ? _defaultSortBy : sortBy;

        // TryGetValue (no _columns[key]): una clave desconocida es un 400 esperado, no una excepción.
        if (!_columns.TryGetValue(key, out var column))
        {
            return QueryErrors.InvalidSortBy(key, _columns.Keys);
        }

        var direction = sortDirection?.ToLowerInvariant() switch
        {
            null or "" or "asc" => "ASC",
            "desc" => "DESC",
            _ => null,
        };

        if (direction is null)
        {
            return QueryErrors.InvalidSortDirection;
        }

        // Solo texto nuestro llega al SQL: la columna del diccionario y una dirección fija, nunca key ni sortDirection.
        return Result.Success($"{column} {direction}, {_tieBreaker}");
    }
}
