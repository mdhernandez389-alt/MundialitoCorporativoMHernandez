using Mundialito.Domain.Results;

namespace Mundialito.Application.Abstractions.Querying;

public static class QueryErrors
{
    public static readonly Error InvalidPageNumber =
        Error.Validation("Query.InvalidPageNumber", "pageNumber debe ser 1 o mayor.");

    public static readonly Error InvalidPageSize =
        Error.Validation("Query.InvalidPageSize", $"pageSize debe estar entre 1 y {PagedQuery.MaxPageSize}.");

    public static readonly Error InvalidSortDirection =
        Error.Validation("Query.InvalidSortDirection", "sortDirection debe ser 'asc' o 'desc'.");

    /// <summary>Le dice al cliente qué valores sí acepta, así puede corregir sin leer documentación.</summary>
    public static Error InvalidSortBy(string sortBy, IEnumerable<string> allowed) =>
        Error.Validation(
            "Query.InvalidSortBy",
            $"No se puede ordenar por '{sortBy}'. Valores permitidos: {string.Join(", ", allowed)}.");
}
