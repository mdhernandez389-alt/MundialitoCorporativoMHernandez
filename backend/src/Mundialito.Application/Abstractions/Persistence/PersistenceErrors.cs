using Mundialito.Domain.Results;

namespace Mundialito.Application.Abstractions.Persistence;

/// <summary>
/// Errores que devuelve <see cref="IUnitOfWork.CommitAsync"/> cuando la base rechaza el cambio porque
/// chocó con otro request. Son casos esperados (→ 409), no excepciones.
/// </summary>
public static class PersistenceErrors
{
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("Persistence.ConcurrencyConflict", "Otro usuario modificó estos datos al mismo tiempo. Vuelve a intentarlo.");

    public static readonly Error DuplicateKey =
        Error.Conflict("Persistence.DuplicateKey", "Ya existe un registro con esos datos.");

    public static readonly Error ReferenceConflict =
        Error.Conflict("Persistence.ReferenceConflict", "La operación choca con datos relacionados: el registro está en uso o la referencia ya no existe.");
}
