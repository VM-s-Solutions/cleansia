using System.Text.Json;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The booking catalogue a signed-out visitor browses — the home page, the services page, the guest
/// wizard — over both customer hosts with no token. An entry is offered only when a platform-wide pay
/// config covers it, and a pay config belongs to a company, so the anonymous read is answered in the
/// operator of the market it names, or of the default market when it names none (ADR-0061 D3): company
/// A's CZK catalogue for Czechia, company B's EUR catalogue for Slovakia, never the other company's
/// coverage, and nothing for a country that is not a market — unserviced, or served by a deactivated
/// company (ADR-0064).
/// </summary>
public sealed class AnonymousCatalogueRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string EurId = "cur-eur-anon-catalogue";
    private const string SvkId = "country-svk-anon-catalogue";
    private const string MngId = "country-mng-anon-catalogue";

    private const string CzServiceId = "service-anon-cz";
    private const string SkServiceId = "service-anon-sk";
    private const string PaidOnlyByBServiceId = "service-anon-paid-by-b";
    private const string CzPackageId = "package-anon-cz";
    private const string CzExtraId = "extra-anon-cz";

    private static readonly string[] CatalogueRoutes =
        ["/api/Service/GetOverview", "/api/Package/GetOverview", "/api/Extra/GetOverview"];

    private Task ArrangeAsync() => SeedAsync(async ctx =>
    {
        await DomainSeed.EnsureReferenceDataAsync(ctx);

        var eur = Currency.Create("EUR", "€", "Euro");
        eur.Id = EurId;
        eur.IsActive = true;
        ctx.Currencies.Add(eur);

        var slovakia = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
        slovakia.Id = SvkId;
        var mongolia = Country.Create("Mongolia", "MNG", "MN", isServiced: false);
        mongolia.Id = MngId;
        ctx.Countries.AddRange(slovakia, mongolia);
        ctx.CountryConfigurations.Add(
            CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m).AssignOperator(HostTestTenants.B));

        var category = ServiceCategory.Create("anon-catalogue", "Anonymous catalogue", "Category under test");
        ctx.Add(category);

        var czService = Service.Create(category.Id, "Czech clean", "Under test", 60);
        czService.Id = CzServiceId;
        var skService = Service.Create(category.Id, "Slovak clean", "Under test", 60);
        skService.Id = SkServiceId;
        var paidOnlyByB = Service.Create(category.Id, "Paid by the other company", "Under test", 60);
        paidOnlyByB.Id = PaidOnlyByBServiceId;
        ctx.AddRange(czService, skService, paidOnlyByB);
        ctx.ServicePrices.AddRange(
            ServicePrice.Create(CzServiceId, DomainSeed.CurrencyId, 900m, 100m),
            ServicePrice.Create(SkServiceId, EurId, 40m, 10m),
            ServicePrice.Create(PaidOnlyByBServiceId, DomainSeed.CurrencyId, 900m, 100m));

        var package = Package.Create("Czech bundle", "Under test");
        package.Id = CzPackageId;
        package.AddService(czService);
        ctx.Packages.Add(package);
        ctx.PackagePrices.Add(PackagePrice.Create(CzPackageId, DomainSeed.CurrencyId, 1500m));

        var extra = Extra.Create("anon-oven", "Oven", "Under test");
        extra.Id = CzExtraId;
        ctx.Extras.Add(extra);
        ctx.ExtraPrices.Add(ExtraPrice.Create(CzExtraId, DomainSeed.CurrencyId, 250m));

        var skPay = EmployeePayConfig.CreateForService(SkServiceId, 10m, EurId);
        skPay.TenantId = HostTestTenants.B;
        var czkPayOfB = EmployeePayConfig.CreateForService(PaidOnlyByBServiceId, 300m, DomainSeed.CurrencyId);
        czkPayOfB.TenantId = HostTestTenants.B;
        ctx.EmployeePayConfigs.AddRange(
            EmployeePayConfig.CreateForService(CzServiceId, 300m, DomainSeed.CurrencyId),
            EmployeePayConfig.CreateForPackage(CzPackageId, 500m, DomainSeed.CurrencyId),
            skPay,
            czkPayOfB);
    });

    private static async Task<string[]> IdsAsync(HttpClient guest, string route)
    {
        var response = await guest.GetAsync(route);
        HttpAssert.IsOk(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.EnumerateArray()
            .Select(entry => entry.GetProperty("id").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_guest_naming_no_market_is_offered_the_default_markets_catalogue_as_its_operator_pays_it(bool mobile)
    {
        await ArrangeAsync();
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var guest = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();

        Assert.Equal(new[] { CzServiceId }, await IdsAsync(guest, "/api/Service/GetOverview"));
        Assert.Equal(new[] { CzPackageId }, await IdsAsync(guest, "/api/Package/GetOverview"));
        Assert.Equal(new[] { CzExtraId }, await IdsAsync(guest, "/api/Extra/GetOverview"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_guest_naming_a_market_is_offered_that_markets_catalogue_as_its_operator_pays_it(bool mobile)
    {
        await ArrangeAsync();
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var guest = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();

        var czechia = $"?countryId={DomainSeed.CountryId}";
        Assert.Equal(new[] { CzServiceId }, await IdsAsync(guest, "/api/Service/GetOverview" + czechia));
        Assert.Equal(new[] { CzPackageId }, await IdsAsync(guest, "/api/Package/GetOverview" + czechia));
        Assert.Equal(new[] { CzExtraId }, await IdsAsync(guest, "/api/Extra/GetOverview" + czechia));
        Assert.Equal(new[] { SkServiceId }, await IdsAsync(guest, $"/api/Service/GetOverview?countryId={SvkId}"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_guest_naming_an_unserviced_country_is_offered_nothing(bool mobile)
    {
        await ArrangeAsync();
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var guest = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();

        foreach (var route in CatalogueRoutes)
        {
            Assert.Empty(await IdsAsync(guest, $"{route}?countryId={MngId}"));
            Assert.Empty(await IdsAsync(guest, $"{route}?countryId=country-nowhere"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_guest_naming_the_market_of_a_deactivated_company_is_offered_nothing(bool mobile)
    {
        await ArrangeAsync();
        await SeedAsync(async ctx =>
            (await ctx.Tenants.SingleAsync(t => t.Id == HostTestTenants.B)).Deactivate("hosttests", DateTimeOffset.UtcNow));
        using var mobileHost = new HostTestApplicationFactory<Cleansia.Web.Mobile.Customer.Program>(Db.ConnectionString);
        using var guest = mobile ? mobileHost.CreateClient() : CustomerClientAnonymous();

        Assert.Empty(await IdsAsync(guest, $"/api/Service/GetOverview?countryId={SvkId}"));
        Assert.Equal(new[] { CzServiceId }, await IdsAsync(guest, "/api/Service/GetOverview"));
    }
}
