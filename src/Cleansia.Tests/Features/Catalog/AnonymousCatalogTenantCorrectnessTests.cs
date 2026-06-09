using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.ServiceAreas;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// ADR-0001 Addendum A1 read-parity behavioural tests for the five anonymous catalog entities.
///
/// Spins a REAL <see cref="CleansiaDbContext"/> (so <see cref="CleansiaDbContext.OnModelCreating"/> and
/// the global tenant query filter actually run) over SQLite in-memory. The scenario is the one the panel
/// flagged: catalog rows seeded under TWO different tenants, then read the way an ANONYMOUS customer reads
/// them (no JWT ⇒ no tenant_id claim ⇒ <see cref="ITenantProvider.GetCurrentTenantId"/> returns null).
///
/// While the entities were <see cref="Cleansia.Core.Domain.Common.ITenantEntity"/> the filter collapsed
/// the anonymous read to <c>TenantId == null</c> and returned only the null-tenant slice — RED. As platform
/// config the filter no longer applies and the anonymous read returns the FULL catalog across both tenants.
/// </summary>
public sealed class AnonymousCatalogTenantCorrectnessTests : IDisposable
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";
    private const string CountryId = "country-cz";

    private readonly SqliteConnection _connection;

    public AnonymousCatalogTenantCorrectnessTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    /// <summary>
    /// Seed one catalog row of each kind under <paramref name="tenantId"/>. Created with that tenant in
    /// context, so while the entities were ITenantEntity <see cref="CleansiaDbContext.CommitAsync"/> stamped
    /// TenantId on insert; as platform config there is nothing to stamp. Returns the ids/slug needed by the
    /// pricing path so the cross-tenant rows can be resolved by an anonymous quote.
    /// </summary>
    private async Task<(string ServiceId, string PackageId, string ExtraSlug)> SeedCatalogForTenantAsync(
        string tenantId, string suffix)
    {
        await using var ctx = NewContext(tenantId);
        await ctx.Database.EnsureCreatedAsync();

        if (!await ctx.Countries.IgnoreQueryFilters().AnyAsync(c => c.Id == CountryId))
        {
            var country = Country.Create("Czechia", "CZE", isServiced: true);
            country.Id = CountryId;
            ctx.Countries.Add(country);
        }

        var category = ServiceCategory.Create($"cat-{suffix}", $"Category {suffix}", "seeded");
        var service = Service.Create(category.Id, $"Service {suffix}", "seeded", 1000m, 200m);
        var package = Package.Create($"Package {suffix}", "seeded", 500m);
        var extra = Extra.Create($"extra-{suffix}", $"Extra {suffix}", "seeded", 50m);
        var city = ServiceCity.Create(CountryId, $"City {suffix}");

        ctx.ServiceCategories.Add(category);
        ctx.Services.Add(service);
        ctx.Packages.Add(package);
        ctx.Extras.Add(extra);
        ctx.ServiceCities.Add(city);
        await ctx.CommitAsync(CancellationToken.None);

        return (service.Id, package.Id, extra.Slug);
    }

    private async Task SeedTwoTenantCatalogAsync()
    {
        await SeedCatalogForTenantAsync(TenantA, "A");
        await SeedCatalogForTenantAsync(TenantB, "B");
    }

    [Fact]
    public async Task ServiceOverview_Anonymous_ReturnsBothTenantsActiveServices()
    {
        await SeedTwoTenantCatalogAsync();

        await using var ctx = NewContext(tenantId: null);
        var services = await new ServiceRepository(ctx).GetAll()
            .Where(s => s.IsActive)
            .Include(s => s.Category)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(2, services.Count);
        Assert.All(services, s => Assert.NotNull(s.Category));
    }

    [Fact]
    public async Task ServiceCategory_Anonymous_ReturnsBothTenantsCategories()
    {
        // ServiceCategory has no own anonymous controller — it is reached transitively through
        // Service.GetOverview's Include, so it must clear the filter alongside Service.
        await SeedTwoTenantCatalogAsync();

        await using var ctx = NewContext(tenantId: null);
        var categories = await new ServiceCategoryRepository(ctx).GetAll().ToListAsync(CancellationToken.None);

        Assert.Equal(2, categories.Count);
    }

    [Fact]
    public async Task PackageOverview_Anonymous_ReturnsBothTenantsActivePackages()
    {
        await SeedTwoTenantCatalogAsync();

        await using var ctx = NewContext(tenantId: null);
        var packages = await new PackageRepository(ctx).GetAll()
            .Where(p => p.IsActive)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(2, packages.Count);
    }

    [Fact]
    public async Task ExtraOverview_Anonymous_ReturnsBothTenantsActiveExtras()
    {
        await SeedTwoTenantCatalogAsync();

        await using var ctx = NewContext(tenantId: null);
        var extras = await new ExtraRepository(ctx).GetAll()
            .Where(e => e.IsActive)
            .ToListAsync(CancellationToken.None);

        Assert.Equal(2, extras.Count);
    }

    [Fact]
    public async Task ServiceCities_Anonymous_ReturnsBothTenantsActiveCities()
    {
        await SeedTwoTenantCatalogAsync();

        await using var ctx = NewContext(tenantId: null);
        var cities = await new ServiceCityRepository(ctx).GetByCountryAsync(CountryId, CancellationToken.None);

        Assert.Equal(2, cities.Count);
    }

    [Fact]
    public async Task QuotePricing_Anonymous_ResolvesCatalogRowsAcrossTenants()
    {
        // The anonymous Order/Quote pricing read (OrderPricingCalculator) resolves Service/Package by id
        // and Extra by slug. Under the collapse it could only see the null-tenant slice; as platform config
        // it resolves any catalog row regardless of which tenant created it.
        var (serviceA, packageA, extraSlugA) = await SeedCatalogForTenantAsync(TenantA, "A");
        var (serviceB, packageB, extraSlugB) = await SeedCatalogForTenantAsync(TenantB, "B");

        await using var ctx = NewContext(tenantId: null);

        var services = await new ServiceRepository(ctx)
            .GetByIds([serviceA, serviceB]).ToListAsync(CancellationToken.None);
        var packages = await new PackageRepository(ctx)
            .GetByIds([packageA, packageB]).ToListAsync(CancellationToken.None);
        var extras = await new ExtraRepository(ctx).GetAll()
            .Where(e => e.IsActive && new[] { extraSlugA, extraSlugB }.Contains(e.Slug))
            .ToListAsync(CancellationToken.None);

        Assert.Equal(2, services.Count);
        Assert.Equal(2, packages.Count);
        Assert.Equal(2, extras.Count);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
