using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Currencies;

/// <summary>
/// THE ONE DEFINITION OF AN OFFERABLE CURRENCY, against real Postgres: the row exists, it is switched
/// on, AND the catalogue carries at least one price row in it — a service, a package or an extra.
///
/// <para>Three surfaces depend on this answer agreeing with itself: the quote and create validators
/// refuse a caller-named currency that fails it, and <c>SetDefaultCurrency</c> refuses to promote
/// one. So the set a customer can book in and the set an admin can star are the same set, and this
/// is where that set is pinned. Each of the three price tables gets its own case, because a predicate
/// that consulted only two of them would look identical in every other test.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CurrencyOfferabilityTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private CleansiaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString())
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));
    }

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
    }

    private sealed record Seeded(string CurrencyId, string ServiceId, string PackageId, string ExtraId);

    /// <summary>One currency and one unpriced entry of each kind. The case adds the price row it is about.</summary>
    private async Task<Seeded> SeedAsync(bool active)
    {
        await using var ctx = NewContext();

        var currency = Currency.Create("EUR", "€", "Euro");
        currency.IsActive = active;
        var category = ServiceCategory.Create("cat", "Category", "d");
        var service = Service.Create(category.Id, "Service", "d", 60);
        var package = Package.Create("Package", "d");
        var extra = Extra.Create("inside-oven", "Inside oven", null);

        ctx.Currencies.Add(currency);
        ctx.Add(category);
        ctx.Add(service);
        ctx.Add(package);
        ctx.Add(extra);
        await ctx.CommitAsync(CancellationToken.None);

        return new Seeded(currency.Id, service.Id, package.Id, extra.Id);
    }

    private async Task<bool> IsOfferableAsync(string currencyId)
    {
        await using var ctx = NewContext();
        return await new CurrencyRepository(ctx).IsOfferableAsync(currencyId, CancellationToken.None);
    }

    [Fact]
    public async Task An_Active_Currency_With_No_Price_Rows_Is_Not_Offerable()
    {
        await ResetAsync();
        var seeded = await SeedAsync(active: true);

        Assert.False(await IsOfferableAsync(seeded.CurrencyId));
    }

    [Fact]
    public async Task A_Service_Price_Alone_Makes_It_Offerable()
    {
        await ResetAsync();
        var seeded = await SeedAsync(active: true);
        await using (var ctx = NewContext())
        {
            ctx.Add(ServicePrice.Create(seeded.ServiceId, seeded.CurrencyId, 40m, 0m));
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.True(await IsOfferableAsync(seeded.CurrencyId));
    }

    [Fact]
    public async Task A_Package_Price_Alone_Makes_It_Offerable()
    {
        await ResetAsync();
        var seeded = await SeedAsync(active: true);
        await using (var ctx = NewContext())
        {
            ctx.Add(PackagePrice.Create(seeded.PackageId, seeded.CurrencyId, 20m));
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.True(await IsOfferableAsync(seeded.CurrencyId));
    }

    [Fact]
    public async Task An_Extra_Price_Alone_Makes_It_Offerable()
    {
        await ResetAsync();
        var seeded = await SeedAsync(active: true);
        await using (var ctx = NewContext())
        {
            ctx.Add(ExtraPrice.Create(seeded.ExtraId, seeded.CurrencyId, 5m));
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.True(await IsOfferableAsync(seeded.CurrencyId));
    }

    /// <summary>
    /// The switch wins over the rows. The seed prices EUR ahead of the flip on purpose — an admin
    /// authors the catalogue in it while it is still off — and none of that makes it bookable.
    /// </summary>
    [Fact]
    public async Task A_Priced_But_Switched_Off_Currency_Is_Not_Offerable()
    {
        await ResetAsync();
        var seeded = await SeedAsync(active: false);
        await using (var ctx = NewContext())
        {
            ctx.Add(ServicePrice.Create(seeded.ServiceId, seeded.CurrencyId, 40m, 0m));
            ctx.Add(PackagePrice.Create(seeded.PackageId, seeded.CurrencyId, 20m));
            ctx.Add(ExtraPrice.Create(seeded.ExtraId, seeded.CurrencyId, 5m));
            await ctx.CommitAsync(CancellationToken.None);
        }

        Assert.False(await IsOfferableAsync(seeded.CurrencyId));
    }

    [Fact]
    public async Task A_Currency_That_Does_Not_Exist_Is_Not_Offerable()
    {
        await ResetAsync();

        Assert.False(await IsOfferableAsync("no-such-currency"));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
