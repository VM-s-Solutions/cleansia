using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Cleansia.Core.AppServices.Features.Bookings;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Bookings;
using Cleansia.Core.Domain.Configuration;
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
using MockQueryable;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// The template's Thursday 10:00 is the wall clock where the home is. Prague is on summer time in
    /// September, so the occurrence is 08:00 UTC, not the 10:00 UTC every other test here reads for a
    /// market with no zone.
    /// </summary>
    [Fact]
    public async Task An_Occurrence_Lands_At_The_Homes_Market_Wall_Clock_Time()
    {
        await SeedAsync(marketZone: "Europe/Prague");

        var result = await RunAsync(Now, horizonDays: 7);

        Assert.Equal(1, result.OrdersCreated);
        Assert.Equal([FirstOccurrence.AddHours(-2)], await OccurrencesAsync());
    }

    [Fact]
    public async Task A_Market_With_No_Zone_Of_Its_Own_Reads_The_Default_Markets_Clock()
    {
        await SeedAsync(marketZone: null, defaultMarketZone: "Europe/Prague");

        var result = await RunAsync(Now, horizonDays: 7);

        Assert.Equal(1, result.OrdersCreated);
        Assert.Equal([FirstOccurrence.AddHours(-2)], await OccurrencesAsync());
    }

    /// <summary>
    /// A schedule stored before the server held times to the window — an iOS wheel could send 03:00 — is
    /// preserved but spawns nothing until its owner moves it into the bookable day.
    /// </summary>
    [Fact]
    public async Task A_Template_Timed_Outside_The_Bookable_Day_Creates_Nothing()
    {
        await SeedAsync(timeOfDay: new TimeOnly(3, 0));

        var result = await RunAsync(Now, horizonDays: 14);

        Assert.Equal(0, result.OrdersCreated);
        Assert.Empty(await OccurrencesAsync());
    }

    /// <summary>
    /// Today's 09:00 is an hour away at the 08:00 tick: under the two-hour floor a one-off booking is held
    /// to, so it is skipped rather than created already late, and next week's is the first occurrence.
    /// </summary>
    [Fact]
    public async Task An_Occurrence_Under_The_Minimum_Lead_Time_Is_Skipped()
    {
        await SeedAsync(day: Now.DayOfWeek, timeOfDay: new TimeOnly(9, 0));

        var result = await RunAsync(Now, horizonDays: 8);

        Assert.Equal(1, result.OrdersCreated);
        Assert.Equal([Now.Date.AddDays(7).AddHours(9)], await OccurrencesAsync());
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
            endsOn: template.EndsOn,
            preferredEmployeeId: template.PreferredEmployeeId);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task CancelFirstOccurrenceAsync()
    {
        _tenantProvider.SetTenantOverride(TestTenants.Default);
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
        services.AddSingleton(Cleansia.Tests.Features.Orders.OrderMarketDoubles.OperatedBy(TestTenants.Default));
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
        services.AddScoped<ICountryConfigurationRepository>(
            sp => new CountryConfigurationRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<ICurrencyRepository>(
            sp => new CurrencyRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddScoped<ICurrencyResolutionService>(
            sp => new CurrencyResolutionService(
                new EmployeeRepository(sp.GetRequiredService<CleansiaDbContext>()),
                new CountryConfigurationRepository(sp.GetRequiredService<CleansiaDbContext>()),
                sp.GetRequiredService<ICurrencyRepository>()));
        services.AddScoped<IOrderRepository>(
            sp => new OrderRepository(sp.GetRequiredService<CleansiaDbContext>()));
        services.AddSingleton(PricingCalculator());
        services.AddScoped(sp => RealOrderFactory(sp.GetRequiredService<IOrderRepository>()));
        // The sweep requires a PAID membership (T-0690). These classes are about tenant stamping,
        // dedupe and per-template isolation, so the owner is simply entitled — otherwise the sweep
        // correctly generates nothing and their real subject never runs.
        services.AddScoped(_ => EntitledMemberships());
        services.AddScoped<INotificationProducer>(_ => Mock.Of<INotificationProducer>());
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
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            ExtraRepositoryDouble.Empty(),
            CataloguePriceDoubles.NoServices(),
            CataloguePriceDoubles.NoPackages(),
            CataloguePriceDoubles.NoExtras(),
            PayConfigRepositoryDouble.Holding(),
            new Mock<ICompanyInfoRepository>().Object,
            new Mock<ICountryConfigurationRepository>().Object,
            new Mock<IVatCalculator>().Object,
            loyalty.Object,
            // The sweep now requires a PAID membership (T-0690). This class is not about
            // membership, so the owner is simply entitled and the real subject runs.
            EntitledMemberships(),
            holdResolver.Object,
            WorkContractResolvers.Resolver().Object,
            new Mock<INotificationProducer>().Object,
            Mock.Of<IAdminNotifier>(),
            NullLogger<OrderFactory>.Instance);
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

    private async Task SeedAsync(
        string? marketZone = null, string? defaultMarketZone = null, DayOfWeek? day = null, TimeOnly? timeOfDay = null)
    {
        _tenantProvider.SetTenantOverride(TestTenants.Default);

        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        currency.Id = "currency-czk";
        currency.SetAsDefault(true);
        ctx.Set<Currency>().Add(currency);

        // The service address's country must resolve to a real currency: a named country with no
        // configuration throws rather than falling back to the default.
        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = "country-cz";
        ctx.Set<Country>().Add(country);
        ctx.Set<CountryConfiguration>().Add(CountryConfiguration.Create("country-cz", "CZK", "cs", 0.21m, timeZoneId: marketZone));
        if (defaultMarketZone is not null)
        {
            var defaultCountry = Country.Create("Slovakia", "SK", "SK", isServiced: true);
            defaultCountry.Id = "country-sk";
            ctx.Set<Country>().Add(defaultCountry);
            ctx.Set<CountryConfiguration>().Add(
                CountryConfiguration.Create("country-sk", "EUR", "sk", 0.23m, timeZoneId: defaultMarketZone)
                    .SetAsDefaultMarket(true));
        }

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
            dayOfWeek: day ?? FirstOccurrence.DayOfWeek,
            timeOfDay: timeOfDay ?? new TimeOnly(10, 0),
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
        private string? _tenantId = TestTenants.Default;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }

    private static IUserMembershipRepository EntitledMemberships()
    {
        var memberships = new Mock<IUserMembershipRepository>();
        memberships
            .Setup(r => r.GetEntitledForUserNoTrackingAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) =>
                UserMembershipMockFactory.Paid(userId));
        return memberships.Object;
    }
}
