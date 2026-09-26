using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

public class AddressRepository(CleansiaDbContext context) : BaseRepository<Address>(context), IAddressRepository
{
    public Task<Address?> GetAddressAsync(string street, string city, string zipCode, string countryId, CancellationToken cancellationToken)
    {
        return context.Addresses.FirstOrDefaultAsync(a =>
            a.Street == street &&
            a.City == city &&
            a.ZipCode == zipCode &&
            a.CountryId == countryId, cancellationToken);
    }

    public async Task<IReadOnlySet<string>> GetReferencedElsewhereAsync(
        IReadOnlyCollection<string> addressIds,
        IReadOnlyCollection<string> exceptOrderIds,
        IReadOnlyCollection<string> exceptSavedAddressIds,
        string? exceptEmployeeId,
        CancellationToken cancellationToken)
    {
        if (addressIds.Count == 0)
        {
            return new HashSet<string>();
        }

        var byOrders = await context.Orders.IgnoreQueryFilters()
            .Where(o => addressIds.Contains(o.CustomerAddressId) && !exceptOrderIds.Contains(o.Id))
            .Select(o => o.CustomerAddressId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var bySavedAddresses = await context.SavedAddresses.IgnoreQueryFilters()
            .Where(s => addressIds.Contains(s.AddressId)
                && !exceptSavedAddressIds.Contains(s.Id))
            .Select(s => s.AddressId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var byEmployees = await context.Employees.IgnoreQueryFilters()
            .Where(e => e.AddressId != null && addressIds.Contains(e.AddressId)
                && (exceptEmployeeId == null || e.Id != exceptEmployeeId))
            .Select(e => e.AddressId!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return byOrders.Concat(bySavedAddresses).Concat(byEmployees).ToHashSet(StringComparer.Ordinal);
    }

    public override void RemoveRange(IEnumerable<Address> entities)
    {
        // Detect the replacement navigations before deleting their former principal rows.
        context.ChangeTracker.DetectChanges();
        base.RemoveRange(entities);
    }
}
