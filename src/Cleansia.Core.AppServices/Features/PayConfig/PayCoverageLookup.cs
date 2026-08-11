using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayConfig;

/// <summary>
/// Loads the two halves <see cref="PayCoverage"/> compares — the catalogue entries in question and the
/// pay configs that could apply — so every gate asks the coverage question through one query shape
/// rather than four. Static, in the feature that owns pay configuration, mirroring
/// <c>OrderPayEstimator</c>: shared read logic with no state and nothing to register.
/// </summary>
public static class PayCoverageLookup
{
    /// <summary>
    /// The core question, over a catalogue the caller already holds. <paramref name="employeeId"/> null
    /// asks the platform-wide question — the answer that holds for every cleaner, including one who has
    /// not registered yet.
    /// </summary>
    public static async Task<IReadOnlyList<PayCoverageTarget>> FindGapsAsync(
        IEmployeePayConfigRepository payConfigRepository,
        IReadOnlyList<PayCoverageTarget> catalogue,
        string? employeeId,
        CancellationToken cancellationToken)
    {
        if (catalogue.Count == 0)
        {
            return [];
        }

        var serviceIds = catalogue
            .Where(target => target.Kind == PayCoverageTargetKind.Service)
            .Select(target => target.Id)
            .ToList();

        var packageIds = catalogue
            .Where(target => target.Kind == PayCoverageTargetKind.Package)
            .Select(target => target.Id)
            .ToList();

        // Narrowed in SQL to the rows that could possibly apply, then decided in memory by
        // PayCoverage.Applies — the SQL term is a superset filter for cost, never the arbiter, so the
        // two cannot drift into disagreement about what "applies" means.
        var query = payConfigRepository.GetAll()
            .Where(config =>
                (config.ServiceId != null && serviceIds.Contains(config.ServiceId))
                || (config.PackageId != null && packageIds.Contains(config.PackageId)));

        query = employeeId is null
            ? query.Where(config => config.EmployeeId == null)
            : query.Where(config => config.EmployeeId == null || config.EmployeeId == employeeId);

        var configs = await query.AsNoTracking().ToListAsync(cancellationToken);

        return PayCoverage.FindGaps(catalogue, configs, employeeId);
    }

    /// <summary>
    /// Gaps over the ACTIVE catalogue. Active is the whole definition of "catalogue" here:
    /// <c>Service</c> and <c>Package</c> carry no geography — <c>Country.IsServiced</c> and
    /// <c>ServiceCity</c> scope ADDRESSES, not catalogue entries — so <c>IsActive</c> is the only axis,
    /// and it is the same one the customer booking wizard already filters on.
    /// </summary>
    public static async Task<IReadOnlyList<PayCoverageTarget>> FindActiveCatalogueGapsAsync(
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository,
        IEmployeePayConfigRepository payConfigRepository,
        string? employeeId,
        CancellationToken cancellationToken)
    {
        var services = await serviceRepository.GetAll()
            .Where(service => service.IsActive)
            .AsNoTracking()
            .Select(service => new CatalogueRow(service.Id, service.Name))
            .ToListAsync(cancellationToken);

        var packages = await packageRepository.GetAll()
            .Where(package => package.IsActive)
            .AsNoTracking()
            .Select(package => new CatalogueRow(package.Id, package.Name))
            .ToListAsync(cancellationToken);

        return await FindGapsAsync(
            payConfigRepository,
            AsTargets(services, packages),
            employeeId,
            cancellationToken);
    }

    /// <summary>
    /// Gaps over one booking's selection, asked platform-wide because the order lands on EVERY
    /// cleaner's board. Deliberately not filtered by <c>IsActive</c>: a deactivated entry is still
    /// bookable by id today (<c>ExistWithIdsAsync</c> carries no active term) and a recurring template
    /// holds its ids in a JSON column with no FK, so gating on the active catalogue here would leave
    /// both routes able to mint an unquotable order.
    /// </summary>
    public static async Task<IReadOnlyList<PayCoverageTarget>> FindSelectionGapsAsync(
        IServiceRepository serviceRepository,
        IPackageRepository packageRepository,
        IEmployeePayConfigRepository payConfigRepository,
        IEnumerable<string> serviceIds,
        IEnumerable<string> packageIds,
        CancellationToken cancellationToken)
    {
        var wantedServiceIds = serviceIds.Distinct().ToList();
        var wantedPackageIds = packageIds.Distinct().ToList();

        var services = wantedServiceIds.Count == 0
            ? []
            : await serviceRepository.GetByIds(wantedServiceIds)
                .AsNoTracking()
                .Select(service => new CatalogueRow(service.Id, service.Name))
                .ToListAsync(cancellationToken);

        var packages = wantedPackageIds.Count == 0
            ? []
            : await packageRepository.GetByIds(wantedPackageIds)
                .AsNoTracking()
                .Select(package => new CatalogueRow(package.Id, package.Name))
                .ToListAsync(cancellationToken);

        return await FindGapsAsync(
            payConfigRepository,
            AsTargets(services, packages),
            employeeId: null,
            cancellationToken);
    }

    private static List<PayCoverageTarget> AsTargets(
        List<CatalogueRow> services, List<CatalogueRow> packages) =>
        services
            .Select(row => new PayCoverageTarget(PayCoverageTargetKind.Service, row.Id, row.Name))
            .Concat(packages.Select(row => new PayCoverageTarget(PayCoverageTargetKind.Package, row.Id, row.Name)))
            .ToList();

    private sealed record CatalogueRow(string Id, string Name);
}
