using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
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
/// Real <see cref="CleansiaDbContext"/> over SQLite in-memory proving the in-use guard also covers
/// the non-FK reference class: a catalog id stored inside a <see cref="RecurringBookingTemplate"/>'s
/// JSON id column. No foreign key can guard those references, so <c>IsInUseAsync</c> is the only thing
/// that stops a deleted catalog item leaving a dangling JSON id that materializes into broken
/// recurring bookings. The template lives in another tenant than the (tenantless, platform-config)
/// catalog row, so the check must read templates across all tenants.
/// </summary>
public sealed class CatalogInUseTemplateGuardTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CatalogInUseTemplateGuardTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    private async Task<(string TemplatedServiceId, string FreeServiceId, string TemplatedPackageId, string FreePackageId)> SeedAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        ctx.Add(Cleansia.Core.Domain.Internationalization.Language.Create("en", "English"));

        var category = ServiceCategory.Create("cat-1", "Category", "seeded");
        var templatedService = Service.Create(category.Id, "Templated Service", "seeded", 1000m, 200m);
        var freeService = Service.Create(category.Id, "Free Service", "seeded", 1000m, 200m);
        var templatedPackage = Package.Create("Templated Package", "seeded", 500m);
        var freePackage = Package.Create("Free Package", "seeded", 500m);

        var user = User.CreateWithPassword("plus@cleansia.test", "Passw0rd!", "Plus", "User");
        user.Id = "user-1";

        ctx.ServiceCategories.Add(category);
        ctx.Services.Add(templatedService);
        ctx.Services.Add(freeService);
        ctx.Packages.Add(templatedPackage);
        ctx.Packages.Add(freePackage);
        ctx.Add(user);

        var template = RecurringBookingTemplate.Create(
            userId: user.Id,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: System.DayOfWeek.Tuesday,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: "addr-1",
            selectedServiceIds: [templatedService.Id],
            selectedPackageIds: [templatedPackage.Id],
            paymentType: PaymentType.Card,
            startsOn: DateTime.UtcNow);
        ctx.Add(template);

        await ctx.CommitAsync(CancellationToken.None);

        return (templatedService.Id, freeService.Id, templatedPackage.Id, freePackage.Id);
    }

    [Fact]
    public async Task ServiceReferencedByTemplateJson_ReportsInUse()
    {
        var (templatedServiceId, _, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new ServiceRepository(ctx).IsInUseAsync(templatedServiceId, CancellationToken.None);

        Assert.True(inUse);
    }

    [Fact]
    public async Task ServiceNotReferencedByAnyTemplate_IsDeletable()
    {
        var (_, freeServiceId, _, _) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new ServiceRepository(ctx).IsInUseAsync(freeServiceId, CancellationToken.None);

        Assert.False(inUse);
    }

    [Fact]
    public async Task PackageReferencedByTemplateJson_ReportsInUse()
    {
        var (_, _, templatedPackageId, _) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new PackageRepository(ctx).IsInUseAsync(templatedPackageId, CancellationToken.None);

        Assert.True(inUse);
    }

    [Fact]
    public async Task PackageNotReferencedByAnyTemplate_IsDeletable()
    {
        var (_, _, _, freePackageId) = await SeedAsync();

        await using var ctx = NewContext();
        var inUse = await new PackageRepository(ctx).IsInUseAsync(freePackageId, CancellationToken.None);

        Assert.False(inUse);
    }

    [Fact]
    public async Task TemplateInAnotherTenant_StillReportsServiceInUse()
    {
        var (templatedServiceId, _, _, _) = await SeedAsync();

        // An admin acting under a different tenant claim must still see the cross-tenant template
        // reference: the catalog row is platform config shared by every tenant.
        await using var ctx = NewContext(tenantId: "tenant-other");
        var inUse = await new ServiceRepository(ctx).IsInUseAsync(templatedServiceId, CancellationToken.None);

        Assert.True(inUse);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
