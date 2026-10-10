using Mundialito.Domain.Results;

namespace Mundialito.UnitTests.Domain;

/// <summary>Result Pattern (§5): éxito o fallo con código, mensaje y tipo; sin excepciones como control de flujo.</summary>
public sealed class ResultTests
{
    private static readonly Error Conflict = Error.Conflict("Test.Conflict", "Conflicto de prueba.");

    [Fact]
    public void Success_no_tiene_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_lleva_codigo_mensaje_y_tipo()
    {
        var result = Result.Failure(Conflict);

        Assert.True(result.IsFailure);
        Assert.Equal("Test.Conflict", result.Error.Code);
        Assert.Equal("Conflicto de prueba.", result.Error.Message);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void Success_con_valor_expone_el_valor()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Un_Error_se_convierte_implicitamente_en_un_Result_fallido()
    {
        // Es lo que permite escribir "return TeamErrors.NotFound(id);" en un método que devuelve Result<Team>.
        Result<int> result = Conflict;

        Assert.True(result.IsFailure);
        Assert.Equal(Conflict, result.Error);
    }

    [Fact]
    public void Leer_el_valor_de_un_fallo_es_un_bug_y_lanza()
    {
        Result<int> result = Conflict;

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Un_fallo_sin_error_es_un_bug_y_lanza()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure(Error.None));
    }

    [Theory]
    [InlineData("", "Mensaje")]
    [InlineData("Codigo", " ")]
    public void Un_error_exige_codigo_y_mensaje(string code, string message)
    {
        Assert.ThrowsAny<ArgumentException>(() => Error.Validation(code, message));
    }

    [Fact]
    public void Errores_con_los_mismos_datos_son_iguales()
    {
        Assert.Equal(Error.NotFound("Team.NotFound", "No existe."), Error.NotFound("Team.NotFound", "No existe."));
    }
}
