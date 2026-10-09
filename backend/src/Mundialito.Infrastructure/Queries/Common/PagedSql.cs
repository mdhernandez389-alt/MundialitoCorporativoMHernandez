using System.Data.Common;
using Dapper;
using Mundialito.Application.Abstractions.Querying;
using Mundialito.Domain.Results;

namespace Mundialito.Infrastructure.Queries.Common;

/// <summary>
/// Ejecuta un listado paginado en la base (§10): valida paginación y orden, y trae el total y la página
/// en un solo viaje.
/// </summary>
internal static class PagedSql
{
    /// <param name="select">Lista de columnas (<c>SELECT t.Id, t.Name</c>). Texto fijo, nunca del usuario.</param>
    /// <param name="from">FROM con sus JOIN. Texto fijo, nunca del usuario.</param>
    public static async Task<Result<PagedResult<T>>> QueryAsync<T>(
        DbConnection connection,
        PagedQuery query,
        SortMap sorting,
        string select,
        string from,
        SqlWhere where,
        CancellationToken cancellationToken)
    {
        var paging = query.ValidatePaging();
        if (paging.IsFailure)
        {
            return paging.Error;
        }

        var orderBy = sorting.Resolve(query.SortBy, query.SortDirection);
        if (orderBy.IsFailure)
        {
            return orderBy.Error;
        }

        // Dos consultas, un viaje: COUNT para el total y OFFSET/FETCH para la página.
        // Solo viajan pageSize filas; la tabla nunca se trae completa a memoria.
        var sql = $"""
            SELECT COUNT(*) {from} {where};

            {select}
            {from}
            {where}
            ORDER BY {orderBy.Value}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        // long: pageNumber muy grande × pageSize desbordaría un int.
        where.Parameters.Add("Offset", (long)(query.PageNumber - 1) * query.PageSize);
        where.Parameters.Add("PageSize", query.PageSize);

        using var results = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, where.Parameters, cancellationToken: cancellationToken));

        var totalRecords = await results.ReadSingleAsync<int>();
        var data = (await results.ReadAsync<T>()).AsList();

        return Result.Success(new PagedResult<T>(data, query.PageNumber, query.PageSize, totalRecords));
    }
}
