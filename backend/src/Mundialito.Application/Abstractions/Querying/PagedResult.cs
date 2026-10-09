namespace Mundialito.Application.Abstractions.Querying;

/// <summary>
/// Respuesta de todo listado, con la forma exacta que pide el §10:
/// <c>{ data, pageNumber, pageSize, totalRecords, totalPages }</c>.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Data, int PageNumber, int PageSize, int TotalRecords)
{
    public int TotalPages => (int)Math.Ceiling(TotalRecords / (double)PageSize);
}
