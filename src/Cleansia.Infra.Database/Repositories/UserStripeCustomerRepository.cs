using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class UserStripeCustomerRepository(CleansiaDbContext context)
    : BaseRepository<UserStripeCustomer>(context), IUserStripeCustomerRepository
{
    public Task<UserStripeCustomer?> GetForUserInCurrencyAsync(string userId, string currencyId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.CurrencyId == currencyId, cancellationToken);
    }

    public Task<bool> IsStripeCustomerIdClaimedAsync(string stripeCustomerId, CancellationToken cancellationToken)
    {
        return GetDbSet()
            .IgnoreQueryFilters()
            .AnyAsync(c => c.StripeCustomerId == stripeCustomerId, cancellationToken);
    }

    public async Task RemoveForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var rows = await GetDbSet().IgnoreQueryFilters().Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        GetDbSet().RemoveRange(rows);
    }
}
