using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface ISavedCardRepository : IRepository<SavedCard, string>
{
    /// <summary>The user's active captured cards, with their currency.</summary>
    Task<IReadOnlyList<SavedCard>> GetCapturedForUserAsync(string userId, CancellationToken cancellationToken);

    /// <summary>The owner's active captured cards in one currency, across operators; the caller must pin the owner from a JWT or an authorized stored record.</summary>
    Task<IReadOnlyList<SavedCard>> GetCapturedForUserInCurrencyAsync(string userId, string currencyId, CancellationToken cancellationToken);

    /// <summary>The Stripe webhook's read: it carries no tenant claim, so the row is found across tenants.</summary>
    Task<SavedCard?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken);

    /// <summary>Erasure: every row of the user, loaded and removed inside the caller's unit of work.</summary>
    Task RemoveForUserIgnoringTenantAsync(string userId, CancellationToken cancellationToken);
}
