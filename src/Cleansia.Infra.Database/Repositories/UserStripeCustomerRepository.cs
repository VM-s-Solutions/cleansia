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

    public async Task<string?> FindUserIdByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken)
    {
        var perCurrency = await GetDbSet()
            .IgnoreQueryFilters()
            .Where(c => c.StripeCustomerId == stripeCustomerId)
            .Select(c => c.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        if (perCurrency is not null)
        {
            return perCurrency;
        }

        return await Context.Users
            .IgnoreQueryFilters()
            .Where(u => u.StripeCustomerId == stripeCustomerId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
