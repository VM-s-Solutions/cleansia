namespace Cleansia.Core.Domain.Repositories;

/// <summary>
/// The two scalars an ownership-scoped currency rule needs off an order -- who placed it and what
/// it is priced in -- projected without the order graph. <see cref="UserId"/> is null for a guest
/// booking, as on the row.
/// </summary>
public sealed record OrderOwnerAndCurrency(string? UserId, string CurrencyId);
