namespace Mundialito.Domain.Results;

/// <summary>
/// Describe un fallo esperado del negocio. Inmutable y comparable por valor (record).
/// </summary>
/// <remarks>
/// Solo se crea con las fábricas <see cref="Validation"/>, <see cref="NotFound"/> y <see cref="Conflict"/>,
/// así todo error lleva siempre código, mensaje y tipo.
/// </remarks>
public sealed record Error
{
    /// <summary>Representa la ausencia de error en un <see cref="Result"/> exitoso.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    private Error(string code, string message, ErrorType type)
    {
        Code = code;
        Message = message;
        Type = type;
    }

    /// <summary>Código estable y legible por máquinas, p. ej. <c>"Team.NotFound"</c>.</summary>
    public string Code { get; }

    /// <summary>Descripción para personas.</summary>
    public string Message { get; }

    public ErrorType Type { get; }

    public static Error Validation(string code, string message) => Create(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => Create(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => Create(code, message, ErrorType.Conflict);

    private static Error Create(string code, string message, ErrorType type)
    {
        // Un código o mensaje vacío es un bug del programador, no un caso de negocio.
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new Error(code, message, type);
    }
}
