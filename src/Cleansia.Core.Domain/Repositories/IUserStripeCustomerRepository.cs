using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Repositories;

public interface IUserStripeCustomerRepository : IRepository<UserStripeCustomer, string>
{
    Task<UserStripeCustomer?> GetForUserInCurrencyAsync(string userId, string currencyId, CancellationToken cancellationToken);

    /// <summary>Whether any row, in any tenant, already claims this Stripe Customer for a currency.</summary>
    Task<bool> IsStripeCustomerIdClaimedAsync(string stripeCustomerId, CancellationToken cancellationToken);

    /// <summary>Erasure: the rows go with the legacy field <c>User.Anonymize</c> clears. Loaded and removed inside the caller's unit of work.</summary>
    Task RemoveForUserAsync(string userId, CancellationToken cancellationToken);
}
