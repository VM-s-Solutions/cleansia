using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Whether a recurring selection may pay cash, judged on the live catalogue by the same
/// <see cref="OrderDuration"/> sum the order factory staffs every occurrence with. A template always
/// belongs to an account, so only the crew term of <see cref="BookingPolicy.AllowsCash"/> can fail.
/// Loaded once for any number of selections, so the recurring list costs two reads, not two per template.
/// </summary>
internal sealed class RecurringCashEligibility
{
    private readonly Dictionary<string, Service> _services;
    private readonly Dictionary<string, Package> _packages;

    private RecurringCashEligibility(Dictionary<string, Service> services, Dictionary<string, Package> packages)
    {
        _services = services;
        _packages = packages;
    }

    public static async Task<RecurringCashEligibility> LoadAsync(
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository,
        IEnumerable<string> serviceIds,
        IEnumerable<string> packageIds,
        CancellationToken cancellationToken)
    {
        var services = await serviceRepository
            .GetByIds(serviceIds.Distinct().ToList())
            .ToListAsync(cancellationToken);
        var packages = await packageRepository
            .GetByIds(packageIds.Distinct().ToList())
            .Include(p => p.IncludedServices)
                .ThenInclude(s => s.Service)
            .ToListAsync(cancellationToken);

        return new RecurringCashEligibility(
            services.ToDictionary(s => s.Id),
            packages.ToDictionary(p => p.Id));
    }

    /// <summary>
    /// Ids are counted once each, as the factory's <c>GetByIds</c> load counts them; an id the catalogue
    /// does not hold adds nothing here and is refused by the factory's own gates.
    /// </summary>
    public bool Allows(IEnumerable<string> serviceIds, IEnumerable<string> packageIds)
        => BookingPolicy.AllowsCash(
            signedIn: true,
            OrderDuration.RequiredEmployees(OrderDuration.EstimateMinutes(
                serviceIds.Distinct().Where(_services.ContainsKey).Select(id => _services[id]),
                packageIds.Distinct().Where(_packages.ContainsKey).Select(id => _packages[id]))));
}
