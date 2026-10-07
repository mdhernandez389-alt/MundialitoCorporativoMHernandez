namespace Mundialito.Domain.Results;

/// <summary>
/// Resultado de una operación que no devuelve valor: éxito, o fallo con un <see cref="Results.Error"/>.
/// Reemplaza el uso de excepciones como control de flujo.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        // Estas combinaciones solo pueden venir de un bug: las fábricas nunca las producen.
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("Un resultado exitoso no puede tener error.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("Un resultado fallido debe tener un error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>El error si falló; <see cref="Error.None"/> si fue exitoso.</summary>
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>Permite <c>return TeamErrors.NotFound;</c> en un método que devuelve <see cref="Result"/>.</summary>
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Resultado de una operación que devuelve un <typeparamref name="TValue"/> cuando es exitosa.
/// </summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// El valor del éxito. Leerlo en un fallo es un bug (hay que revisar <see cref="Result.IsSuccess"/> antes).
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");

    /// <summary>Permite <c>return TeamErrors.NotFound;</c> en un método que devuelve <see cref="Result{TValue}"/>.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
