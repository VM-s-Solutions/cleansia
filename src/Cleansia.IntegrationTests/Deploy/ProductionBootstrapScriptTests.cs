using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Addresses.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.IntegrationTests.Features.Legal;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Service = Cleansia.Core.Domain.Services.Service;

namespace Cleansia.IntegrationTests.Deploy;

/// <summary>
/// sql-scripts/prod-bootstrap.sql first runs for real on the freshly migrated production database (runbook
/// P7), which no agent may touch. So it runs here on the migrated schema emptied of every row, the
/// tenant registry included, twice; and then a guest quotes and books once an administrator has typed in
/// the smallest catalogue, because an anonymous booking resolves its market to the operating company the
/// bootstrap alone provides.
/// </summary>
[Collection("PostgresCollection")]
public class ProductionBootstrapScriptTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CategoryId = "bootstrap-category";
    private const string ServiceId = "bootstrap-service";
    private const decimal ServicePriceCzk = 1200m;

    private static readonly string[] ReferenceTables =
    [
        "Tenants", "Languages", "Countries", "ServiceCities", "Currencies", "CountryInvoiceConfigs",
        "CountryConfigurations", "EmployeeDocumentRequirements", "EmailTemplateTranslations",
        "LoyaltyTierConfigs", "PropertySizePresets",
    ];

    private static readonly string[] OwnerTypedTables =
    [
        "Users", "Orders", "PromoCodes", "CompanyInfo", "MembershipPlans", "MembershipPlanPrices",
        "ServiceCategories", "Services", "Packages", "Extras", "ServicePrices", "PackagePrices",
        "ExtraPrices", "EmployeePayConfigs",
    ];

    [Fact]
    public async Task A_second_run_changes_nothing()
    {
        await TestMethod(
            arrange: async (CleansiaDbContext _) => await RunBootstrapOnEmptyDatabaseAsync(),
            act: async _ =>
            {
                var first = await CountRowsAsync(ReferenceTables);
                await RunBootstrapAsync();
                return new[] { first, await CountRowsAsync(ReferenceTables) };
            },
            assert: (CleansiaDbContext _, Dictionary<string, long>[] runs) =>
            {
                Assert.All(ReferenceTables, table => Assert.True(runs[0][table] > 0, $"{table} is empty after the bootstrap"));
                Assert.Equal(runs[0], runs[1]);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task It_opens_the_czech_market_under_its_operating_company_and_writes_nothing_the_owner_types_in()
    {
        await TestMethod(
            arrange: async (CleansiaDbContext _) => await RunBootstrapOnEmptyDatabaseAsync(),
            act: async _ => await CountRowsAsync(OwnerTypedTables),
            assert: async (CleansiaDbContext context, Dictionary<string, long> ownerTyped) =>
            {
                Assert.Equal(TestTenants.Default, Assert.Single(await context.Tenants.ToListAsync()).Id);

                var czechia = Assert.Single(await context.Countries.Where(c => c.IsServiced).ToListAsync());
                Assert.Equal("CZE", czechia.IsoCode);
                var market = Assert.Single(await context.CountryConfigurations.ToListAsync());
                Assert.Equal(czechia.Id, market.CountryId);
                Assert.Equal(TestTenants.Default, market.OperatorTenantId);
                Assert.True(market.IsDefaultMarket);
                Assert.Equal("CZK", market.DefaultCurrencyCode);

                var czk = Assert.Single(await context.Currencies.ToListAsync());
                Assert.Equal("CZK", czk.Code);
                Assert.True(czk.IsActive && czk.IsDefault);

                Assert.Contains(
                    await context.EmployeeDocumentRequirements.Where(r => r.CountryId == czechia.Id).ToListAsync(),
                    r => r.DocumentType == DocumentType.InsuranceDocument && !r.IsRequired);
                Assert.Equal(4, await context.LoyaltyTierConfigs.CountAsync());

                Assert.All(OwnerTypedTables, table => Assert.Equal(0L, ownerTyped[table]));
            },
            transactional: false);
    }

    [Fact]
    public async Task A_guest_quotes_and_books_once_an_administrator_adds_a_minimal_catalogue()
    {
        await TestMethod(
            setup: ConfigureGuestSession,
            arrange: async (CleansiaDbContext context) =>
            {
                await RunBootstrapOnEmptyDatabaseAsync();
                await LegalSeed.SeedAsync(context);

                var czkId = await context.Currencies.Where(c => c.Code == "CZK").Select(c => c.Id).SingleAsync();
                var category = ServiceCategory.Create("home", "Home", "Home cleaning");
                category.Id = CategoryId;
                var service = Service.Create(CategoryId, "General Cleaning", "Standard cleaning", 120);
                service.Id = ServiceId;
                context.AddRange(category, service);
                context.ServicePrices.Add(ServicePrice.Create(ServiceId, czkId, ServicePriceCzk, 0m));
                context.EmployeePayConfigs.Add(EmployeePayConfig.CreateForService(ServiceId, ServicePriceCzk / 2, czkId));
                StampUnstampedAdded(context, TestTenants.Default);
                await context.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var czechiaId = await provider.GetRequiredService<CleansiaDbContext>().Countries
                    .Where(c => c.IsoCode == "CZE").Select(c => c.Id).SingleAsync();
                var mediator = provider.GetRequiredService<IMediator>();

                var quote = await mediator.Send(new QuoteOrder.Command(
                    [ServiceId], [], Rooms: 2, Bathrooms: 1, CurrencyId: null, CountryId: czechiaId));
                Assert.True(quote.IsSuccess, $"QuoteOrder failed with: {quote.Error?.Message}");

                var booking = await mediator.Send(new CreateOrder.Command(
                    CustomerName: "Bootstrap Guest",
                    CustomerEmail: "bootstrap-guest@cleansia.test",
                    CustomerPhone: "+420777111222",
                    CustomerAddress: new AddressDto("Vodičkova 1", "Praha", "11000", czechiaId, null),
                    SavedAddressId: null,
                    SelectedPackageIds: [],
                    SelectedServiceIds: [ServiceId],
                    Rooms: 2,
                    Bathrooms: 1,
                    Extras: new Dictionary<string, bool>(),
                    CleaningDate: DateTime.UtcNow.Date.AddDays(3).AddHours(9),
                    PaymentType: PaymentType.Card,
                    CurrencyId: quote.Value.CurrencyId,
                    TotalPrice: quote.Value.TotalPrice,
                    TermsAccepted: true,
                    EarlyPerformanceRequested: true));
                return (Quote: quote.Value, Booking: booking);
            },
            assert: async (CleansiaDbContext context, (QuoteOrder.Response Quote, BusinessResult<CreateOrder.Response> Booking) result) =>
            {
                Assert.Equal("CZK", result.Quote.CurrencyCode);
                Assert.Equal(ServicePriceCzk, result.Quote.TotalPrice);

                Assert.True(result.Booking.IsSuccess, $"CreateOrder failed with: {string.Join("; ", (result.Booking as IValidationResult)?.Errors.Select(e => $"{e.Code}={e.Message}") ?? [result.Booking.Error?.Message])}");
                var order = await context.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == result.Booking.Value.Id);
                Assert.Equal(TestTenants.Default, order.TenantId);
                Assert.Equal(result.Quote.CurrencyId, order.CurrencyId);
                Assert.Equal(ServicePriceCzk, order.TotalPrice);
                Assert.Null(order.UserId);
            },
            transactional: false);
    }

    // TestMethod has emptied every table and put the fixture's two companies back; the registry goes too,
    // so the bootstrap is the only thing that creates the operating company.
    private async Task RunBootstrapOnEmptyDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        await using var clear = new NpgsqlCommand("DELETE FROM \"Tenants\"", connection);
        await clear.ExecuteNonQueryAsync();
        await RunBootstrapAsync();
    }

    private async Task RunBootstrapAsync()
    {
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(BootstrapScript(), connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Dictionary<string, long>> CountRowsAsync(IEnumerable<string> tables)
    {
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        var counts = new Dictionary<string, long>();
        foreach (var table in tables)
        {
            await using var count = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{table}\"", connection);
            counts[table] = (long)(await count.ExecuteScalarAsync())!;
        }

        return counts;
    }

    private static string BootstrapScript()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0)
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "Could not locate the solution directory from the test base directory.");
        return File.ReadAllText(Path.GetFullPath(Path.Combine(dir!.FullName, "..", "sql-scripts", "prod-bootstrap.sql")));
    }

    // Mobile, so the card booking mints no Stripe Checkout session; no claim and no override, so the
    // scope behaviour resolves the market's operating company as it does for any anonymous request.
    private static Task ConfigureGuestSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>())));
        services.Replace(ServiceDescriptor.Singleton<IOrderChannelProvider>(
            _ => new OrderChannelProvider(OrderChannel.Mobile)));
        services.Replace(ServiceDescriptor.Scoped<IAddressGeocoder, NoopAddressGeocoder>());
        return Task.CompletedTask;
    }

    private sealed class NoopAddressGeocoder : IAddressGeocoder
    {
        public Task PopulateCoordinatesAsync(Address address, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
