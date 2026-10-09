using Mundialito.Domain.Results;

namespace Mundialito.Application.Abstractions.Querying;

/// <summary>
/// Parámetros que todo listado acepta (§10). Si el cliente no los envía, se usan los valores por defecto.
/// </summary>
public abstract record PagedQuery
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 100;

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Nombre público del campo (p. ej. <c>name</c>). Cada listado define cuáles acepta.</summary>
    public string? SortBy { get; init; }

    /// <summary><c>asc</c> (por defecto) o <c>desc</c>.</summary>
    public string? SortDirection { get; init; }

    /// <summary>
    /// El tope de <see cref="PageSize"/> es lo que garantiza "nunca traer todo a memoria":
    /// ni pidiendo <c>pageSize=1000000</c> se lee la tabla completa.
    /// </summary>
    public Result ValidatePaging()
    {
        if (PageNumber < 1)
        {
            return QueryErrors.InvalidPageNumber;
        }

        if (PageSize is < 1 or > MaxPageSize)
        {
            return QueryErrors.InvalidPageSize;
        }

        return Result.Success();
    }
}
