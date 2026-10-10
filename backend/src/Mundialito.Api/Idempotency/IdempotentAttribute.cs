using Microsoft.AspNetCore.Mvc;

namespace Mundialito.Api.Idempotency;

/// <summary>
/// Marca un POST como idempotente: exige el header <c>Idempotency-Key</c> y repite la respuesta original
/// ante un reintento. Ver <see cref="IdempotencyFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter));
