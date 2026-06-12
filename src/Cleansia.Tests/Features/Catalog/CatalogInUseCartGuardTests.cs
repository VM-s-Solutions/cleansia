using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Catalog;

/// <summary>
/// Spins a REAL <see cref="CleansiaDbContext"/> over SQLite in-memory so the in-use guard runs against
/// the actual EF pipeline and the real Cart item tables. Proves a catalog row sitting in a live,
/// server-persisted customer cart reports in-use (so it cannot be deleted into an orphaned cart line),
/// while an unreferenced row is deletable.
/// </summary>
public sealed class CatalogInUseCartGuardTests : IDisposable
{
    private const string CountryId = "country-cz";

    private readonly SqliteConnection _connection;

    public CatalogInUseCartGuardTests()
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

    private async Task<(string CartedServiceId, string FreeServiceId, string CartedPackageId, string FreePackageId)> SeedAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(Cleansia.Core.Domain.Internationalization.Language.Create("en", "English"));

        var category = ServiceCategory.Create("cat-1", "Category", "seeded");
        var cartedService = Service.Create(category.Id, "Carted Service", "seeded", 1000m, 200m);
        var freeService = Service.Create(category.Id, "Free Service", "seeded", 1000m, 200m);
        var cartedPackage = Package.Create("Carted Package", "seeded", 500m);
        var freePackage = Package.Create("Free Package", "seeded", 500m);

        var user = User.CreateWithPassword("buyer@cleansia.test", "Passw0rd!", "Buyer", "User");
        user.Id = "user-1";

        ctx.ServiceCategories.Add(category);
        ctx.Services.Add(cartedService);
        ctx.Services.Add(freeService);
        ctx.Packages.Add(cartedPackage);
        ctx.Packages.Add(freePackage);
        ctx.Add(user);

        var cart = Cart.CreateWithUser(user);
        cart.AddService(cartedService, 2);
        cart.AddPackage(cartedPackage, 1);
        ctx.Add(cart);

        await ctx.CommitAsync(CancellationToken.None);

        return (cartedService.Id, freeService.Id, cartedPackage.Id, freePackage.Id);
    }

    [Fact]
    public async Task ServiceInCart_ReportsInUse()
    {
        var (cartedServiceId, _, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new ServiceRepository(ctx).IsInUseAsync(cartedServiceId, CancellationToken.None);

        Assert.True(inUse);
    }

    [Fact]
    public async Task ServiceNotReferenced_IsDeletable()
    {
        var (_, freeServiceId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new ServiceRepository(ctx).IsInUseAsync(freeServiceId, CancellationToken.None);

        Assert.False(inUse);
    }

    [Fact]
    public async Task PackageInCart_ReportsInUse()
    {
        var (_, _, cartedPackageId, _) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new PackageRepository(ctx).IsInUseAsync(cartedPackageId, CancellationToken.None);

        Assert.True(inUse);
    }

    [Fact]
    public async Task PackageNotReferenced_IsDeletable()
    {
        var (_, _, _, freePackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new PackageRepository(ctx).IsInUseAsync(freePackageId, CancellationToken.None);

        Assert.False(inUse);
    }

    private sealed class NullTenantProvider : ITenantProvider
    {
        public string? GetCurrentTenantId() => null;
        public void SetTenantOverride(string tenantId) { }
        public void ClearTenantOverride() { }
    }
}
