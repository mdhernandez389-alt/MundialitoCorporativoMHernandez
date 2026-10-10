namespace Mundialito.Application.Abstractions.Observability;

/// <summary>
/// CorrelationId del request en curso (§7). Lo fija la Api al recibir el request y lo lee Infrastructure
/// para guardarlo junto a cada evento de dominio: así un evento lleva el request que lo generó.
/// </summary>
/// <remarks>
/// Fuera de un request HTTP (p. ej. el seed al iniciar) no hay CorrelationId: vale <c>null</c>.
/// </remarks>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
