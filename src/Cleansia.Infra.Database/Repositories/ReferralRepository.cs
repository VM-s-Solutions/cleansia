using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class ReferralRepository(CleansiaDbContext context)
    : BaseRepository<Referral>(context), IReferralRepository
{
    public Task<Referral?> GetByReferredUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<Referral?>(null);
        }

        return GetDbSet()
            .FirstOrDefaultAsync(r => r.ReferredUserId == userId, cancellationToken);
    }

    public Task<Referral?> GetForOrderOwnerAsync(string orderId, string userId, CancellationToken cancellationToken)
        => GetDbSet().IgnoreQueryFilters().Include(r => r.ReferralCode)
            .FirstOrDefaultAsync(r => r.ReferredUserId == userId
                && context.Orders.IgnoreQueryFilters().Any(o => o.Id == orderId && o.UserId == userId), cancellationToken);

    public async Task<IReadOnlyDictionary<ReferralStatus, int>> GetStatusCountsByReferrerAsync(
        string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new Dictionary<ReferralStatus, int>();
        }

        var grouped = await GetDbSet()
            .Where(r => r.ReferrerUserId == userId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(x => x.Status, x => x.Count);
    }

    public async Task<IReadOnlyList<Referral>> GetExpirableAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        return await GetDbSet()
            .Where(r => r.Status == ReferralStatus.Accepted && r.HoldReasons == null && r.AcceptedOn < cutoff)
            .ToListAsync(cancellationToken);
    }

    // The two accounts can be customers of different companies, so every read ignores the tenant filter.
    public async Task<(IReadOnlyList<(string CountryId, string ZipCode, string City, string Street, string? Apartment)> Addresses,
        IReadOnlyList<string> Phones,
        string? Email)> GetContactFootprintAsync(string userId, CancellationToken cancellationToken)
    {
        var orders = await context.Orders.IgnoreQueryFilters()
            .Where(o => o.UserId == userId)
            .Select(o => new
            {
                o.CustomerAddress!.CountryId,
                o.CustomerAddress.ZipCode,
                o.CustomerAddress.City,
                o.CustomerAddress.Street,
                o.CustomerApartment,
                o.CustomerPhone,
            })
            .ToListAsync(cancellationToken);

        var saved = await context.SavedAddresses.IgnoreQueryFilters()
            .Where(s => s.UserId == userId && s.IsActive)
            .Select(s => new { s.Address!.CountryId, s.Address.ZipCode, s.Address.City, s.Address.Street, s.Apartment })
            .ToListAsync(cancellationToken);

        var user = await context.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => new { u.PhoneNumber, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        var addresses = orders.Select(o => (o.CountryId, o.ZipCode, o.City, o.Street, o.CustomerApartment))
            .Concat(saved.Select(s => (s.CountryId, s.ZipCode, s.City, s.Street, s.Apartment)))
            .ToList();
        var phones = orders.Select(o => o.CustomerPhone)
            .Append(user?.PhoneNumber)
            .OfType<string>()
            .ToList();

        return (addresses, phones, user?.Email);
    }

    public async Task<(IReadOnlyList<Referral> Items, int Total)> GetPagedAdminAsync(
        ReferralStatus? status,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = GetDbSet().AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(r => r.AcceptedOn >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(r => r.AcceptedOn <= dateTo.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.AcceptedOn)
            .Skip(offset)
            .Take(limit)
            .Include(r => r.Referrer)
            .Include(r => r.Referred)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<Referral>> GetByUserAsync(
        string userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<Referral>();
        }

        return await GetDbSet()
            .AsNoTracking()
            .Where(r => r.ReferrerUserId == userId || r.ReferredUserId == userId)
            .OrderByDescending(r => r.AcceptedOn)
            .Include(r => r.Referrer)
            .Include(r => r.Referred)
            .Include(r => r.ReferrerCreditCurrency)
            .Include(r => r.ReferredCreditCurrency)
            .ToListAsync(cancellationToken);
    }
}
