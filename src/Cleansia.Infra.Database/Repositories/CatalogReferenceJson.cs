using Microsoft.EntityFrameworkCore;

namespace Cleansia.Infra.Database.Repositories;

/// <summary>
/// Detects a catalog id referenced inside a <see cref="Core.Domain.Bookings.RecurringBookingTemplate"/>'s
/// JSON-array id columns (<c>SelectedServiceIds</c>/<c>SelectedPackageIds</c>), where no foreign key
/// exists to guard the reference. The columns carry a JSON value converter, so they cannot be filtered in
/// SQL across both the Npgsql and SQLite providers; the id collections are materialized and matched in
/// memory. Reads ignore the tenant query filter because the catalog row is tenantless platform config —
/// a reference held by ANY tenant's template counts.
/// </summary>
internal static class CatalogReferenceJson
{
    private const string SelectedServiceIdsField = "_selectedServiceIds";
    private const string SelectedPackageIdsField = "_selectedPackageIds";

    public static Task<bool> IsReferencedByTemplateServiceAsync(
        CleansiaDbContext context, string serviceId, CancellationToken cancellationToken)
        => IsReferencedAsync(context, SelectedServiceIdsField, serviceId, cancellationToken);

    public static Task<bool> IsReferencedByTemplatePackageAsync(
        CleansiaDbContext context, string packageId, CancellationToken cancellationToken)
        => IsReferencedAsync(context, SelectedPackageIdsField, packageId, cancellationToken);

    private static async Task<bool> IsReferencedAsync(
        CleansiaDbContext context, string jsonField, string id, CancellationToken cancellationToken)
    {
        // The public SelectedServiceIds/SelectedPackageIds are EF-ignored computed projections; the
        // mapped, JSON-converted data lives on the private backing field, so project that. Materializing
        // the column applies the read converter (text -> List<string>) client-side; the Contains then
        // runs in memory.
        var idLists = await context.RecurringBookingTemplates
            .IgnoreQueryFilters()
            .Select(t => EF.Property<List<string>>(t, jsonField))
            .ToListAsync(cancellationToken);

        return idLists.Any(ids => ids.Contains(id));
    }
}
