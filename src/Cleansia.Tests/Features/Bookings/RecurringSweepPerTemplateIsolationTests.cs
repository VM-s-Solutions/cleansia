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
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// One permanently-bad template must not starve every template ordered after it. Before the per-template
/// scope split, a throw anywhere in the sweep propagated out of the loop: the templates it had not
/// reached yet were skipped on that tick — and on every later tick too, because a deterministically-bad
/// template sorts the same way every time. A customer's recurring schedule could therefore stop
/// generating forever because of an unrelated customer's broken row.
///
/// <para><b>Why the fix had to be a DI scope and not a <c>try/catch</c>.</b> This suite is the executable
/// argument. The naive catch-and-continue reaches for
/// <c>CleansiaDbContext.Rollback()</c>, which sets every tracked entry to <c>Unchanged</c> — and
/// <c>Added -> Unchanged</c> is NOT <c>Detached</c>. The failed template's half-built order would stop
/// being an insert and STAY in the shared change tracker as a phantom EXISTING row, which the next
/// template's commit then drags along. <see cref="No_Half_Built_Order_From_The_Failed_Template_Is_Persisted"/>
/// is the assertion that catches that: it fails for the <c>Rollback()</c> implementation and passes for
/// the scope-per-template one, because there the wreckage is disposed with its scope and no later commit
/// can ever observe it.</para>
///
/// <para>The failing template is broken by a factory decorator that throws AFTER the real
/// <see cref="OrderFactory"/> has added the order and its status track to the scope's tracker — the exact
/// mid-write state the shared-context version mishandles. Anything that threw before the write would
/// prove nothing.</para>
/// </summary>
public sealed class RecurringSweepPerTemplateIsolationTests : IDisposable
{
    private const string GoodBefore = "tmpl-good-before";
    private const string Bad = "tmpl-bad";
    private const string GoodAfter = "tmpl-good-after";

    private readonly SqliteConnection _connection;
    private readonly MutableTenantProvider _tenantProvider = new();

    public RecurringSweepPerTemplateIsolationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // The sweep's failure isolation is the subject, not referential integrity.
        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// The template seeded BEFORE the bad one and the one seeded AFTER it both materialize. The "after"
    /// case is the starvation regression proper; the "before" case pins that the fix did not trade one
    /// failure mode for another by, say, discarding work already done.
    /// </summary>
    [Fact]
    public async Task A_Failing_Template_Does_Not_Starve_The_Templates_Around_It()
    {
        await SeedAsync(GoodBefore, Bad, GoodAfter);

        var response = await RunSweepAsync();

        Assert.Equal(2, response.TemplatesProcessed);
        Assert.Equal(1, response.TemplatesFailed);
        Assert.True(response.OrdersCreated > 0);

        Assert.NotEmpty(await OrdersForAsync(GoodBefore));
        Assert.NotEmpty(await OrdersForAsync(GoodAfter));
    }

    /// <summary>
    /// The half-built order the failing template left in its change tracker must never reach the database
    /// — not on its own commit (there isn't one) and not smuggled into a LATER template's commit, which is
    /// exactly what <c>Rollback()</c>-based catch-and-continue would do.
    /// </summary>
    [Fact]
    public async Task No_Half_Built_Order_From_The_Failed_Template_Is_Persisted()
    {
        await SeedAsync(GoodBefore, Bad, GoodAfter);

        var response = await RunSweepAsync();

        // Guards this test against passing vacuously: the decorator throws only AFTER the real factory has
        // returned an order, so a recorded failure proves the half-built row really existed in the tracker.
        Assert.Equal(1, response.TemplatesFailed);

        Assert.Empty(await OrdersForAsync(Bad));

        // And nothing anonymous leaked either: every persisted order belongs to one of the two good
        // templates, so no orphaned row rode along on someone else's transaction.
        await using var ctx = NewContext();
        var all = await ctx.Orders.IgnoreQueryFilters().ToListAsync();
        Assert.All(all, o => Assert.Contains(o.RecurringTemplateId, new[] { GoodBefore, GoodAfter }));
    }

    /// <summary>
    /// The failed template keeps its previous marker, so the next tick recomputes the same occurrences and
    /// retries it rather than skipping them forever. The successful ones advance — that is what makes the
    /// replay a no-op for them.
    /// </summary>
    [Fact]
    public async Task The_Failed_Template_Keeps_Its_Marker_And_The_Others_Advance()
    {
        await SeedAsync(GoodBefore, Bad, GoodAfter);

        await RunSweepAsync();

        Assert.Null(await MarkerOfAsync(Bad));
        Assert.NotNull(await MarkerOfAsync(GoodBefore));
        Assert.NotNull(await MarkerOfAsync(GoodAfter));
    }

    /// <summary>
    /// Re-running the sweep is a no-op for the templates that already succeeded (their marker moved past
    /// the horizon) and another attempt for the one that failed — the idempotency contract, still per
    /// template after the split.
    /// </summary>
    [Fact]
    public async Task A_Second_Tick_Replays_The_Successful_Templates_As_No_Ops()
    {
        await SeedAsync(GoodBefore, Bad, GoodAfter);

        var first = await RunSweepAsync();
        var countAfterFirst = (await OrdersForAsync(GoodBefore)).Count + (await OrdersForAsync(GoodAfter)).Count;

        var second = await RunSweepAsync();
        var countAfterSecond = (await OrdersForAsync(GoodBefore)).Count + (await OrdersForAsync(GoodAfter)).Count;

        Assert.Equal(countAfterFirst, countAfterSecond);
        Assert.Equal(0, second.OrdersCreated);

        // The bad template is retried on every tick — it is never silently dropped from the sweep.
        Assert.Equal(1, first.TemplatesFailed);
        Assert.Equal(1, second.TemplatesFailed);
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            _tenantProvider);

    private async Task<List<Order>> OrdersForAsync(string templateId)
    {
        await using var ctx = NewContext();
        return await ctx.Orders
            .IgnoreQueryFilters()
            .Where(o => o.RecurringTemplateId == templateId)
            .ToListAsync();
    }

    private async Task<DateTime?> MarkerOfAsync(string templateId)
    {
        await using var ctx = NewContext();
        var template = await ctx.Set<RecurringBookingTemplate>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == templateId);
        return template.LastMaterializedFor;
    }

    private async Task<MaterializeRecurringBookings.Response> RunSweepAsync()
    {
        _tenantProvider.ClearTenantOverride();

        await using var provider = BuildProvider();
        using var outerScope = provider.CreateScope();

        var handler = new MaterializeRecurringBookings.Handler(
            outerScope.ServiceProvider.GetRequiredService<IRecurringBookingTemplateRepository>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MaterializeRecurringBookings.Handler>.Instance);

        var result = await handler.Handle(new MaterializeRecurringBookings.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        return result.Value!;
    }

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
        services.AddSingleton(PricingCalculator());
        services.AddScoped<IOrderFactory>(sp =>
            new ThrowsForTemplate(RealOrderFactory(sp.GetRequiredService<CleansiaDbContext>()), Bad));
        services.AddScoped<MaterializeRecurringBookingTemplate.Handler>();

        services.AddScoped<IMediator>(sp =>
        {
            var mediator = new Mock<IMediator>();
            mediator
                .Setup(m => m.Send(
                    It.IsAny<MaterializeRecurringBookingTemplate.Command>(), It.IsAny<CancellationToken>()))
                .Returns((MaterializeRecurringBookingTemplate.Command c, CancellationToken ct) =>
                    sp.GetRequiredService<MaterializeRecurringBookingTemplate.Handler>().Handle(c, ct));
            return mediator.Object;
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Throws AFTER the real factory has written the order + status track into the scope's change tracker.
    /// A decorator that threw first would leave nothing to leak and would let the shared-context version
    /// pass.
    /// </summary>
    private sealed class ThrowsForTemplate(IOrderFactory inner, string badTemplateId) : IOrderFactory
    {
        public async Task<Order> CreateAsync(CreateOrderInput input, CancellationToken cancellationToken)
        {
            var order = await inner.CreateAsync(input, cancellationToken);
            if (input.RecurringTemplateId == badTemplateId)
            {
                throw new InvalidOperationException($"Template {badTemplateId} is permanently broken");
            }
            return order;
        }
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

    private async Task SeedAsync(params string[] templateIds)
    {
        _tenantProvider.ClearTenantOverride();

        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        currency.Id = "currency-czk";
        currency.SetAsDefault(true);
        ctx.Set<Currency>().Add(currency);

        foreach (var templateId in templateIds)
        {
            var userId = $"user-{templateId}";
            var savedAddressId = $"saved-{templateId}";

            var user = User.CreateWithPassword(
                $"{userId}@cleansia.test", "Password1!", "Rita", "Recurring", UserProfile.Customer);
            user.Id = userId;
            ctx.Set<User>().Add(user);

            var address = Address.Create("123 Main St", "Prague", "11000", "country-cz");
            address.Id = $"address-{templateId}";
            ctx.Set<Address>().Add(address);

            var saved = SavedAddress.Create(userId, address.Id, "Home", isDefault: true);
            saved.Id = savedAddressId;
            ctx.Set<SavedAddress>().Add(saved);

            var template = RecurringBookingTemplate.Create(
                userId: userId,
                frequency: RecurrenceFrequency.Weekly,
                dayOfWeek: DateTime.UtcNow.AddDays(2).DayOfWeek,
                timeOfDay: new TimeOnly(10, 0),
                rooms: 2,
                bathrooms: 1,
                savedAddressId: savedAddressId,
                selectedServiceIds: [CreateOrderTestData.ServiceId],
                selectedPackageIds: [],
                paymentType: PaymentType.Cash,
                startsOn: DateTime.UtcNow.AddDays(-7));
            template.Id = templateId;
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
