using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// Visibility contract over a REAL <see cref="CleansiaDbContext"/> (SQLite in-memory, real
/// repositories): a deactivated service/package disappears from the customer-facing overview
/// (the booking wizard catalog) while the row itself survives, and the admin list can target it
/// via the IsActive filter (S10 — no global IsActive filter, admins see all by default).
/// </summary>
public sealed class CatalogActiveVisibilityTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CatalogActiveVisibilityTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new NullTenantProvider());
    }

    private async Task<(string ActiveServiceId, string RetiredServiceId, string ActivePackageId, string RetiredPackageId)> SeedAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var category = ServiceCategory.Create("cat-1", "Category", "seeded");
        var activeService = Service.Create(category.Id, "Active Service", "seeded", 1000m, 200m);
        var retiredService = Service.Create(category.Id, "Retired Service", "seeded", 1000m, 200m);
        retiredService.Deactivated("admin-1", DateTimeOffset.UtcNow);

        var activePackage = Package.Create("Active Package", "seeded", 500m);
        var retiredPackage = Package.Create("Retired Package", "seeded", 500m);
        retiredPackage.Deactivated("admin-1", DateTimeOffset.UtcNow);

        ctx.ServiceCategories.Add(category);
        ctx.Services.AddRange(activeService, retiredService);
        ctx.Packages.AddRange(activePackage, retiredPackage);

        await ctx.CommitAsync(CancellationToken.None);

        return (activeService.Id, retiredService.Id, activePackage.Id, retiredPackage.Id);
    }

    [Fact]
    public async Task DeactivatedService_IsHiddenFromCustomerOverview_ButRowSurvives()
    {
        var (activeServiceId, retiredServiceId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var overview = (await new GetServiceOverview.Handler(new ServiceRepository(ctx))
            .Handle(new GetServiceOverview.Request(), CancellationToken.None)).ToList();

        Assert.Contains(overview, s => s.Id == activeServiceId);
        Assert.DoesNotContain(overview, s => s.Id == retiredServiceId);
        Assert.NotNull(await ctx.Services.FindAsync(retiredServiceId));
    }

    [Fact]
    public async Task DeactivatedPackage_IsHiddenFromCustomerOverview_ButRowSurvives()
    {
        var (_, _, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var overview = (await new GetPackageOverview.Handler(new PackageRepository(ctx))
            .Handle(new GetPackageOverview.Request(), CancellationToken.None)).ToList();

        Assert.Contains(overview, p => p.Id == activePackageId);
        Assert.DoesNotContain(overview, p => p.Id == retiredPackageId);
        Assert.NotNull(await ctx.Packages.FindAsync(retiredPackageId));
    }

    [Fact]
    public async Task AdminServiceFilter_IsActiveFalse_ListsOnlyRetired_NullListsAll()
    {
        var (activeServiceId, retiredServiceId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var repository = new ServiceRepository(ctx);

        var retired = await repository
            .GetFiltered(ServiceSpecification.Create(isActive: false).SatisfiedBy())
            .ToListAsync();
        Assert.Single(retired);
        Assert.Equal(retiredServiceId, retired[0].Id);

        var all = await repository
            .GetFiltered(ServiceSpecification.Create().SatisfiedBy())
            .ToListAsync();
        Assert.Contains(all, s => s.Id == activeServiceId);
        Assert.Contains(all, s => s.Id == retiredServiceId);
    }

    [Fact]
    public async Task AdminPackageFilter_IsActiveFalse_ListsOnlyRetired_NullListsAll()
    {
        var (_, _, activePackageId, retiredPackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var repository = new PackageRepository(ctx);

        var retired = await repository
            .GetFiltered(PackageSpecification.Create(isActive: false).SatisfiedBy())
            .ToListAsync();
        Assert.Single(retired);
        Assert.Equal(retiredPackageId, retired[0].Id);

        var all = await repository
            .GetFiltered(PackageSpecification.Create().SatisfiedBy())
            .ToListAsync();
        Assert.Contains(all, p => p.Id == activePackageId);
        Assert.Contains(all, p => p.Id == retiredPackageId);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
