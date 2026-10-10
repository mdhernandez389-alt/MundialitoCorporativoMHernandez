using Mundialito.Domain.Teams;

namespace Mundialito.UnitTests.Domain;

public sealed class TeamTests
{
    [Fact]
    public void Crear_un_equipo_emite_TeamCreated()
    {
        var team = Team.Create("  Sistemas  ", "TI").Value;

        Assert.Equal("Sistemas", team.Name);
        var domainEvent = Assert.IsType<TeamCreatedDomainEvent>(Assert.Single(team.DomainEvents));
        Assert.Equal(team.Id, domainEvent.TeamId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void El_nombre_es_obligatorio(string? name)
    {
        var result = Team.Create(name!, "TI");

        Assert.Equal(TeamErrors.NameRequired, result.Error);
    }

    [Fact]
    public void El_nombre_tiene_un_largo_maximo()
    {
        var result = Team.Create(new string('x', Team.NameMaxLength + 1), "TI");

        Assert.Equal(TeamErrors.NameTooLong, result.Error);
    }
}
