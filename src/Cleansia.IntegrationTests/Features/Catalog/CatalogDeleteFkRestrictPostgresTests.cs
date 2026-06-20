using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Catalog;

/// <summary>
/// Proves the durable TOCTOU fix against a REAL Postgres (Testcontainers): the catalog-reference FKs are
/// ON DELETE RESTRICT, so a reference inserted AFTER the in-use pre-check passed but BEFORE the delete
/// commits is rejected by the database, and the delete handler maps the 23503 to the existing
/// service.in_use / package.in_use business error — no orphan, no 500.
///
/// The race is made deterministic with a repository whose IsInUseAsync reports not-in-use (modelling the
/// instant the check ran, before the reference existed) while every other call hits the real DB; the
/// reference row is committed before the handler flush, so only the FK (not the handler's own pre-check)
/// can reject the delete. That isolates the durable backstop (the FK plus 23503 mapping) from the
/// fast-path guard.
///
/// The schema is built from the live EF model via EnsureCreated on a dedicated database (NOT the shared
/// migration-applied one) so it exercises the entity-config FK behavior this ticket introduces, ahead of
/// the owner-run ef-migration that lands the same change in the deployed schema.
/// </summary>
[Collection("PostgresCollection")]
public class CatalogDeleteFkRestrictPostgresTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private NpgsqlDataSource _dataSource = default!;

    public CatalogDeleteFkRestrictPostgresTests(PostgresContainerFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_fixture.GetConnectionString())
        {
            Database = "catalog_fk_restrict_test"
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        builder.EnableUnmappedTypes();
        _dataSource = builder.Build();

        await using (var bootstrap = NewContext())
        {
            await bootstrap.Database.EnsureDeletedAsync();
            await bootstrap.Database.EnsureCreatedAsync();
        }

        // The citext/pg_trgm extensions are created by EnsureCreated; reload so the data source's cached
        // type catalog knows them before any consumer connection (mirrors the app's bootstrap).
        await using var conn = await _dataSource.OpenConnectionAsync();
        await conn.ReloadTypesAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var ctx = NewContext())
        {
            await ctx.Database.EnsureDeletedAsync();
        }
        await _dataSource.DisposeAsync();
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(_dataSource).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private async Task<string> SeedServiceAsync()
    {
        await using var ctx = NewContext();
        var category = ServiceCategory.Create("cat-1", "Category", "seeded");
        var service = Service.Create(category.Id, "Lonely Service", "seeded", 1000m, 200m);
        ctx.ServiceCategories.Add(category);
        ctx.Services.Add(service);
        await ctx.CommitAsync(CancellationToken.None);
        return service.Id;
    }

    [Fact]
    public async Task DeleteService_WhenReferenceRacesPastTheCheck_FkRejects_AndMapsToServiceInUse()
    {
        var serviceId = await SeedServiceAsync();

        // The race: a package-bundle reference is committed AFTER the in-use check ran (modelled by the
        // RaceBlind repository below), so only the FK can reject the delete.
        await using (var refCtx = NewContext())
        {
            var package = Package.Create("Bundle", "seeded", 500m);
            refCtx.Packages.Add(package);
            var serviceRef = (await refCtx.Services.FindAsync(serviceId))!;
            package.AddService(serviceRef);
            await refCtx.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repository = new RaceBlindServiceRepository(ctx);

        var result = await new DeleteService.Handler(repository).Handle(
            new DeleteService.Command(serviceId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.ServiceInUse, result.Error!.Message);

        // No orphan, no cascade: the service row survives because the DB refused the delete.
        await using var verifyCtx = NewContext();
        Assert.NotNull(await verifyCtx.Services.FindAsync(serviceId));
        Assert.True(await verifyCtx.PackageServices.AnyAsync(ps => ps.ServiceId == serviceId));
    }

    [Fact]
    public async Task DeleteService_WhenNotReferenced_HardDeletes()
    {
        var serviceId = await SeedServiceAsync();

        await using var ctx = NewContext();
        var result = await new DeleteService.Handler(new ServiceRepository(ctx)).Handle(
            new DeleteService.Command(serviceId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var verifyCtx = NewContext();
        Assert.Null(await verifyCtx.Services.FindAsync(serviceId));
    }

    [Fact]
    public async Task DeletePackage_WhenReferenceRacesPastTheCheck_FkRejects_AndMapsToPackageInUse()
    {
        string packageId;
        await using (var seedCtx = NewContext())
        {
            seedCtx.Add(Language.Create("en", "English"));
            var package = Package.Create("Lonely Package", "seeded", 500m);
            seedCtx.Packages.Add(package);
            await seedCtx.CommitAsync(CancellationToken.None);
            packageId = package.Id;
        }

        // The race: a live cart line references the package AFTER the in-use check ran (modelled by the
        // RaceBlind repository below), so only the FK can reject the delete.
        await using (var refCtx = NewContext())
        {
            var user = User.CreateWithPassword("buyer@cleansia.test", "Passw0rd!", "Buyer", "User");
            var packageRef = (await refCtx.Packages.FindAsync(packageId))!;
            var cart = Cart.CreateWithUser(user);
            cart.AddPackage(packageRef, 1);
            refCtx.Add(user);
            refCtx.Add(cart);
            await refCtx.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var repository = new RaceBlindPackageRepository(ctx);

        var result = await new DeletePackage.Handler(repository).Handle(
            new DeletePackage.Command(packageId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.PackageInUse, result.Error!.Message);

        await using var verifyCtx = NewContext();
        Assert.NotNull(await verifyCtx.Packages.FindAsync(packageId));
        Assert.True(await verifyCtx.CartPackageItems.AnyAsync(cpi => cpi.PackageId == packageId));
    }

    // Real ServiceRepository whose in-use check reports clean, modelling the instant the pre-check ran
    // before the racing reference existed. Everything else hits the real DB.
    private sealed class RaceBlindServiceRepository(CleansiaDbContext context) : ServiceRepository(context)
    {
        public override Task<bool> IsInUseAsync(string serviceId, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }

    private sealed class RaceBlindPackageRepository(CleansiaDbContext context) : PackageRepository(context)
    {
        public override Task<bool> IsInUseAsync(string packageId, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
