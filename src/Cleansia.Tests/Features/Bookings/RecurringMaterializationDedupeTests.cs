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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Editing a recurring template clears <see cref="RecurringBookingTemplate.LastMaterializedFor"/> on
/// purpose — the new schedule may put the next occurrence EARLIER than the previously materialized one,
/// so the materializer has to re-evaluate from scratch. While that watermark was the only idempotency
/// guard, the clear meant an edit re-emitted every occurrence already sitting inside the horizon: a
/// second priced order, and for a card template a second charge, on a slot the customer had already
/// booked once.
///
/// <para>The load-bearing assertion is the PAIR, not the refusal. A materializer that creates nothing at
/// all also refuses duplicates, so <see cref="An_Already_Materialized_Occurrence_Is_Not_Recreated_After_An_Edit_Clears_The_Watermark"/>
/// is only worth its line count next to
/// <see cref="A_Genuinely_New_Occurrence_Is_Still_Created_Alongside_An_Already_Materialized_One"/>,
/// which puts one already-materialized occurrence and one brand-new one in the same window and demands
/// exactly one order out.</para>
///
/// <para>Everything is driven through the real <see cref="MaterializeRecurringBookingTemplate.Handler"/>,
/// the real <see cref="OrderFactory"/>, real repositories and a real <see cref="CleansiaDbContext"/> over
/// SQLite, and the "already materialized" order is produced by the FIRST run of the production handler
/// rather than hand-built — the guard reads a column the materializer writes, so a fixture that wrote it
/// itself would prove the query and not the feature. The edit likewise goes through the production
/// <see cref="RecurringBookingTemplate.UpdateSchedule"/>, which is what actually clears the watermark.</para>
/// </summary>
public sealed class RecurringMaterializationDedupeTests : IDisposable
{
    private const string TemplateId = "tmpl-dedupe";
    private const string UserId = "user-dedupe";
    private const string SavedAddressId = "saved-dedupe";

    /// <summary>
    /// A fixed instant, so the occurrence arithmetic in the assertions is arithmetic and not a race with
    /// the wall clock. <see cref="MaterializeRecurringBookingTemplate.Command"/> takes its own
    /// <c>NowUtc</c>, which is what makes pinning it possible.
    /// </summary>
    private static readonly DateTime Now = new(2026, 9, 14, 8, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime FirstOccurrence = Now.Date.AddDays(3).AddHours(10);
    private static readonly DateTime SecondOccurrence = FirstOccurrence.AddDays(7);

    private readonly SqliteConnection _connection;
    private readonly MutableTenantProvider _tenantProvider = new();

    public RecurringMaterializationDedupeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task An_Already_Materialized_Occurrence_Is_Not_Recreated_After_An_Edit_Clears_The_Watermark()
    {
        await SeedAsync();

        var first = await RunAsync(Now, horizonDays: 7);
        Assert.Equal(1, first.OrdersCreated);
        Assert.Equal([FirstOccurrence], await OccurrencesAsync());

        await EditRoomCountAsync();
        Assert.Null(await MarkerAsync());

        var second = await RunAsync(Now, horizonDays: 7);

        Assert.Equal(0, second.OrdersCreated);
        Assert.Equal([FirstOccurrence], await OccurrencesAsync());
    }

    /// <summary>
    /// The other half of the pair. The 14-day window holds the occurrence the first run already created
    /// AND the next one, so a guard that refuses on the template rather than on the occurrence instant
    /// fails here while the refusal test above stays green.
    /// </summary>
    [Fact]
    public async Task A_Genuinely_New_Occurrence_Is_Still_Created_Alongside_An_Already_Materialized_One()
    {
        await SeedAsync();

        await RunAsync(Now, horizonDays: 7);
        await EditRoomCountAsync();

        var second = await RunAsync(Now, horizonDays: 14);

        Assert.Equal(1, second.OrdersCreated);
        Assert.Equal([FirstOccurrence, SecondOccurrence], await OccurrencesAsync());
    }

    /// <summary>
    /// "Already materialized" is a fact about the SWEEP, not about the order's later lifecycle. Excluding
    /// cancelled orders from the guard would let a template edit resurrect an occurrence the customer
    /// cancelled — and would let it resurrect one that <c>AutoCancelStaleRecurringOrders</c> retracted an
    /// hour before the slot, which is a cleaner dispatched to a cleaning nobody confirmed.
    /// </summary>
    [Fact]
    public async Task A_Cancelled_Prior_Occurrence_Still_Blocks_Recreation()
    {
        await SeedAsync();

        await RunAsync(Now, horizonDays: 7);
        await CancelFirstOccurrenceAsync();
        await EditRoomCountAsync();

        var second = await RunAsync(Now, horizonDays: 7);

        Assert.Equal(0, second.OrdersCreated);
        Assert.Equal([FirstOccurrence], await OccurrencesAsync());
        Assert.Equal(OrderStatus.Cancelled, await SingleOrderStatusAsync());
    }

    /// <summary>
    /// A window in which every occurrence is already materialized still moves the watermark forward.
    /// Without it the template re-derives and re-queries the same fully-materialized window on every
    /// tick, and its <c>LastMaterializedFor</c> — which the customer's own DTO carries — stays null until
    /// some future occurrence finally enters the horizon.
    /// </summary>
    [Fact]
    public async Task The_Watermark_Advances_Over_A_Window_That_Was_Already_Materialized()
    {
        await SeedAsync();

        await RunAsync(Now, horizonDays: 7);
        await EditRoomCountAsync();

        await RunAsync(Now, horizonDays: 7);

        Assert.Equal(FirstOccurrence, await MarkerAsync());
    }

    /// <summary>
    /// The dangerous edit is the ordinary one: a customer changing the room count leaves the schedule
    /// fields untouched, so every occurrence already inside the horizon is an occurrence of the new
    /// schedule too — and <see cref="RecurringBookingTemplate.UpdateSchedule"/> clears the watermark all
    /// the same.
    /// </summary>
    private async Task EditRoomCountAsync()
    {
        await using var ctx = NewContext();
        var template = await ctx.Set<RecurringBookingTemplate>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == TemplateId);

        template.UpdateSchedule(
            frequency: template.Frequency,
            dayOfWeek: template.DayOfWeek,
            timeOfDay: template.TimeOfDay,
            rooms: template.Rooms + 1,
            bathrooms: template.Bathrooms,
            savedAddressId: template.SavedAddressId,
            selectedServiceIds: template.SelectedServiceIds.ToList(),
            selectedPackageIds: template.SelectedPackageIds.ToList(),
            paymentType: template.PaymentType,
            startsOn: template.StartsOn,
            endsOn: template.EndsOn);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task CancelFirstOccurrenceAsync()
    {
        await using var ctx = NewContext();
        var order = await ctx.Orders
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .FirstAsync(o => o.RecurringTemplateId == TemplateId);

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<List<DateTime>> OccurrencesAsync()
    {
        await using var ctx = NewContext();
        return await ctx.Orders
            .IgnoreQueryFilters()
            .Where(o => o.RecurringTemplateId == TemplateId)
            .Select(o => o.CleaningDateTime)
            .OrderBy(d => d)
            .ToListAsync();
    }

    private async Task<OrderStatus> SingleOrderStatusAsync()
    {
        await using var ctx = NewContext();
        var order = await ctx.Orders
            .IgnoreQueryFilters()
            .SingleAsync(o => o.RecurringTemplateId == TemplateId);
        return order.CurrentStatus;
    }

    private async Task<DateTime?> MarkerAsync()
    {
        await using var ctx = NewContext();
        var template = await ctx.Set<RecurringBookingTemplate>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == TemplateId);
        return template.LastMaterializedFor;
    }

    private async Task<MaterializeRecurringBookingTemplate.Response> RunAsync(DateTime nowUtc, int horizonDays)
    {
        _tenantProvider.ClearTenantOverride();

        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<MaterializeRecurringBookingTemplate.Handler>();
        var result = await handler.Handle(
            new MaterializeRecurringBookingTemplate.Command(TemplateId, nowUtc, horizonDays),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private ServiceProvider BuildProvider()
    {
        var session = new TestUserSessionProvider("system", "system@cleansia.test");
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddScoped<ITenantProvider>(_ => new MutableTenantProvider());
        services.AddScoped(sp => new CleansiaDbContext(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            session,
            sp.GetRequiredService<ITenantProvider>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CleansiaDbContext>());

        services.AddScoped<IRecurringBookingTemplateRepository>(
            sp => new RecurringBookingTemplateRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<ISavedAddressRepository>(
            sp => new SavedAddressRepository(sp.GetRequiredService<CleansiaDbContext>(), session));
        services.AddScoped<IAddressRepository>(
            sp => new AddressRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<ICurrencyRepository>(
            sp => new CurrencyRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<IOrderRepository>(
            sp => new OrderRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddSingleton(PricingCalculator());
        services.AddScoped(sp => RealOrderFactory(sp.GetRequiredService<IOrderRepository>()));
        services.AddScoped<MaterializeRecurringBookingTemplate.Handler>();

        return services.BuildServiceProvider();
    }

    private static IOrderFactory RealOrderFactory(IOrderRepository orderRepository)
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
            orderRepository,
            services.Object,
            packages.Object,
            PayConfigRepositoryDouble.Holding(),
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

    private async Task SeedAsync()
    {
        _tenantProvider.ClearTenantOverride();

        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        currency.Id = "currency-czk";
        currency.SetAsDefault(true);
        ctx.Set<Currency>().Add(currency);

        var user = User.CreateWithPassword(
            $"{UserId}@cleansia.test", "Password1!", "Rita", "Recurring", UserProfile.Customer);
        user.Id = UserId;
        ctx.Set<User>().Add(user);

        var address = Address.Create("123 Main St", "Prague", "11000", "country-cz");
        address.Id = "address-dedupe";
        ctx.Set<Address>().Add(address);

        var saved = SavedAddress.Create(UserId, address.Id, "Home", isDefault: true);
        saved.Id = SavedAddressId;
        ctx.Set<SavedAddress>().Add(saved);

        var template = RecurringBookingTemplate.Create(
            userId: UserId,
            frequency: RecurrenceFrequency.Weekly,
            dayOfWeek: FirstOccurrence.DayOfWeek,
            timeOfDay: new TimeOnly(10, 0),
            rooms: 2,
            bathrooms: 1,
            savedAddressId: SavedAddressId,
            selectedServiceIds: [CreateOrderTestData.ServiceId],
            selectedPackageIds: [],
            paymentType: PaymentType.Cash,
            startsOn: Now.AddDays(-7));
        template.Id = TemplateId;
        ctx.Set<RecurringBookingTemplate>().Add(template);

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
