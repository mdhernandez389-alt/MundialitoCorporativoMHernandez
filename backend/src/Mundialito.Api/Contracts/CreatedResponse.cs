namespace Mundialito.Api.Contracts;

/// <summary>Cuerpo de un 201 Created: el Id del recurso nuevo (su URL va en el header Location).</summary>
public sealed record CreatedResponse(Guid Id);
