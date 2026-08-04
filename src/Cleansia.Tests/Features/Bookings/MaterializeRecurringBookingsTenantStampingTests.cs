using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.Tests.Features.Orders;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// S8 — a cross-tenant sweep that creates CHILD rows must commit inside the same iteration that set the
/// tenant override. <c>CleansiaDbContext.CommitAsync</c> stamps <c>TenantId</c> on every <c>Added</c>
/// <see cref="ITenantEntity"/> from the tenant that is ambient AT COMMIT TIME, so the materializer's
/// per-template <c>SetTenantOverride</c>/<c>ClearTenantOverride</c> was decorative while the only commit
/// was the pipeline's deferred one: every order and status track in the batch was stamped with the LAST
/// template's tenant, and tenant A's customer got an order that only tenant B can see.
///
/// <para>The bug is invisible in single-tenant mode (every stamp is null and null is right), which is
/// exactly why it shipped — so this suite seeds two templates with two DIFFERENT non-null tenants plus a
/// legacy null-tenant one, and runs the real repositories, the real <see cref="OrderFactory"/> and a real
/// <see cref="CleansiaDbContext"/> over SQLite, because the stamp lives in the context's commit and not
/// in anything a mock can return.</para>
/// </summary>
public sealed class MaterializeRecurringBookingsTenantStampingTests : IDisposable
{
    private const string TenantA = "tenant-a";
    private const string TenantB = "tenant-b";

    private readonly SqliteConnection _connection;
    private readonly MutableTenantProvider _tenantProvider = new();

    public MaterializeRecurringBookingsTenantStampingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // The sweep's tenancy is the subject, not referential integrity — seeding bare template/address
        // rows without their full country/user graph keeps the fixture to the columns under test.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    [Fact]
    public async Task Each_Templates_Orders_Are_Stamped_With_Their_Own_Tenant()
    {
        await SeedAsync(
            Template("tmpl-a", "user-a", "saved-a", TenantA),
            Template("tmpl-b", "user-b", "saved-b", TenantB));

        var response = await RunSweepAsync();

        Assert.True(response.OrdersCreated > 0);
        Assert.Equal(TenantA, await OrderTenantOfAsync("tmpl-a"));
        Assert.Equal(TenantB, await OrderTenantOfAsync("tmpl-b"));
    }

    [Fact]
    public async Task Each_Templates_Status_Tracks_Are_Stamped_With_Their_Own_Tenant()
    {
        await SeedAsync(
            Template("tmpl-a", "user-a", "saved-a", TenantA),
            Template("tmpl-b", "user-b", "saved-b", TenantB));

        await RunSweepAsync();

        Assert.Equal(TenantA, await StatusTrackTenantOfAsync("tmpl-a"));
        Assert.Equal(TenantB, await StatusTrackTenantOfAsync("tmpl-b"));
    }

    /// <summary>
    /// A legacy null-tenant template processed after a tenanted one must not inherit the override the
    /// previous iteration set — the mirror of the clear-before-set half of the shape.
    /// </summary>
    [Fact]
    public async Task A_Legacy_Null_Tenant_Template_Following_A_Tenanted_One_Stays_Null()
    {
        await SeedAsync(
            Template("tmpl-a", "user-a", "saved-a", TenantA),
            Template("tmpl-legacy", "user-legacy", "saved-legacy", tenantId: null));

        await RunSweepAsync();

        Assert.Equal(TenantA, await OrderTenantOfAsync("tmpl-a"));
        Assert.Null(await OrderTenantOfAsync("tmpl-legacy"));
    }

    private async Task<string?> OrderTenantOfAsync(string templateId)
    {
        await using var ctx = NewContext();
        var orders = await ctx.Orders
            .IgnoreQueryFilters()
            .Where(o => o.RecurringTemplateId == templateId)
            .ToListAsync();

        Assert.NotEmpty(orders);
        return Assert.Single(orders.Select(o => o.TenantId).Distinct());
    }

    private async Task<string?> StatusTrackTenantOfAsync(string templateId)
    {
        await using var ctx = NewContext();
        var orderIds = await ctx.Orders
            .IgnoreQueryFilters()
            .Where(o => o.RecurringTemplateId == templateId)
            .Select(o => o.Id)
            .ToListAsync();

        var tracks = await ctx.Set<OrderStatusTrack>()
            .IgnoreQueryFilters()
            .Where(t => orderIds.Contains(t.OrderId))
            .ToListAsync();

        Assert.NotEmpty(tracks);
        return Assert.Single(tracks.Select(t => t.TenantId).Distinct());
    }

    private async Task<MaterializeRecurringBookings.Response> RunSweepAsync()
    {
        _tenantProvider.ClearTenantOverride();

        await using var ctx = NewContext();
        var handler = new MaterializeRecurringBookings.Handler(
            new RecurringBookingTemplateRepository(ctx),
            new SavedAddressRepository(ctx, new TestUserSessionProvider("system", "system@cleansia.test")),
            new AddressRepository(ctx),
            new CurrencyRepository(ctx),
            PricingCalculator(),
            RealOrderFactory(ctx),
            _tenantProvider,
            ctx,
            NullLogger<MaterializeRecurringBookings.Handler>.Instance);

        var result = await handler.Handle(new MaterializeRecurringBookings.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private static IOrderFactory RealOrderFactory(CleansiaDbContext context)
    {
        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());

        var packages = new Mock<IPackageRepository>();
        packages.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<Package>().AsQueryable().BuildMock());

        var loyalty = new Mock<ILoyaltyService>();
        loyalty.Setup(s => s.ResolveTierDiscountForOrderAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierDiscountResult(0m, null));

        var holdResolver = new Mock<IPreferredCleanerHoldResolver>();
        holdResolver.Setup(r => r.ResolveAsync(
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PreferredCleanerOutcome.Declined(HoldDeclineReason.NoPreference));

        return new OrderFactory(
            new OrderRepository(context),
            services.Object,
            packages.Object,
            new Mock<ICompanyInfoRepository>().Object,
            new Mock<ICountryConfigurationRepository>().Object,
            new Mock<IVatCalculator>().Object,
            loyalty.Object,
            new Mock<IUserMembershipRepository>().Object,
            holdResolver.Object,
            new Mock<INotificationProducer>().Object);
    }

    private static IOrderPricingCalculator PricingCalculator()
    {
        var calculator = new Mock<IOrderPricingCalculator>();
        calculator.Setup(c => c.CalculateAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateOrderTestData.MatchingPricing());
        return calculator.Object;
    }

    private static TemplateFixture Template(string templateId, string userId, string savedAddressId, string? tenantId)
        => new(templateId, userId, savedAddressId, tenantId);

    private sealed record TemplateFixture(string TemplateId, string UserId, string SavedAddressId, string? TenantId);

    private async Task SeedAsync(params TemplateFixture[] fixtures)
    {
        // Seeding runs with NO ambient tenant and stamps each row explicitly, so the fixture cannot
        // borrow the very mechanism under test.
        _tenantProvider.ClearTenantOverride();

        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        currency.Id = "currency-czk";
        currency.SetAsDefault(true);
        ctx.Set<Currency>().Add(currency);

        foreach (var fixture in fixtures)
        {
            var user = User.CreateWithPassword(
                $"{fixture.UserId}@cleansia.test", "Password1!", "Rita", "Recurring", UserProfile.Customer);
            user.Id = fixture.UserId;
            user.TenantId = fixture.TenantId;
            ctx.Set<User>().Add(user);

            var address = Address.Create("123 Main St", "Prague", "11000", "country-cz");
            address.Id = $"address-{fixture.TemplateId}";
            address.TenantId = fixture.TenantId;
            ctx.Set<Address>().Add(address);

            var saved = SavedAddress.Create(fixture.UserId, address.Id, "Home", isDefault: true);
            saved.Id = fixture.SavedAddressId;
            saved.TenantId = fixture.TenantId;
            ctx.Set<SavedAddress>().Add(saved);

            var template = RecurringBookingTemplate.Create(
                userId: fixture.UserId,
                frequency: RecurrenceFrequency.Weekly,
                dayOfWeek: DateTime.UtcNow.AddDays(2).DayOfWeek,
                timeOfDay: new TimeOnly(10, 0),
                rooms: 2,
                bathrooms: 1,
                savedAddressId: fixture.SavedAddressId,
                selectedServiceIds: [CreateOrderTestData.ServiceId],
                selectedPackageIds: [],
                paymentType: PaymentType.Cash,
                startsOn: DateTime.UtcNow.AddDays(-7));
            template.Id = fixture.TemplateId;
            template.TenantId = fixture.TenantId;
            ctx.Set<RecurringBookingTemplate>().Add(template);
        }

        await ctx.CommitAsync(CancellationToken.None);
    }

    private sealed class MutableTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
