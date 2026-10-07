namespace Mundialito.Domain.Results;

/// <summary>
/// Qué tipo de problema ocurrió. El dominio no conoce HTTP: la Api traduce cada tipo a un status code.
/// </summary>
public enum ErrorType
{
    /// <summary>Sin error (solo lo usa <see cref="Error.None"/>).</summary>
    None = 0,

    /// <summary>Datos de entrada o regla de negocio inválidos. La Api lo traduce a 400.</summary>
    Validation = 1,

    /// <summary>El recurso no existe. La Api lo traduce a 404.</summary>
    NotFound = 2,

    /// <summary>La operación choca con el estado actual (duplicado, ya registrado...). La Api lo traduce a 409.</summary>
    Conflict = 3
}
