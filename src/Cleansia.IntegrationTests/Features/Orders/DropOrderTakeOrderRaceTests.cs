using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// A take on a Confirmed two-seat order adds a seat and no status row, so its commit used to carry only the
/// seat's insert: a drop that emptied the crew at the same moment walked the order back to New, the take
/// landed anyway, and the order ended New with a cleaner on it, who could neither set off nor start. The
/// take now stamps the order, so its commit compares the order's tokens, and the drop locks the order's row
/// before it reads the crew. Real Postgres, two connections, both commit orders: whichever writer reaches
/// the row second sees what the first one did.
/// </summary>
[Collection("PostgresCollection")]
public class DropOrderTakeOrderRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CleanerA = "emp-drop-take-a";
    private const string CleanerB = "emp-drop-take-b";
    private const string OrderId = "order-drop-take";
    private const string CurrencyId = "currency-czk-drop-take";
    private const string CountryId = "country-cz-drop-take";
    private const string CustomerId = "user-drop-take-cust";

    private readonly List<AdminEvent> _raised = [];

    /// <summary>
    /// The drop holds the row; the take read the order before the drop committed and waits on the row. Once
    /// the drop lands, the take's stale status fails the check and it rolls back whole: the order is New with
    /// nobody on it, and the cleaner is told the job is gone.
    /// </summary>
    [Fact]
    public async Task A_Take_Waiting_On_A_Drop_That_Empties_The_Crew_Is_Refused_And_Seats_Nobody()
    {
        await ResetAndSeedAsync(crew: [CleanerA]);

        await using var dropContext = NewContext(UserOf(CleanerA));
        var dropped = await DropHandler(dropContext, CleanerA).Handle(new DropOrder.Command(OrderId), CancellationToken.None);
        Assert.True(dropped.IsSuccess, dropped.Error?.Message);
        Assert.Equal(0, dropped.Value!.CrewRemaining);

        await using var takeContext = NewContext(UserOf(CleanerB));
        var take = TakeHandler(takeContext, CleanerB).Handle(
            new TakeOrder.Command(OrderId, "work-contract-text"), CancellationToken.None);
        await WaitUntilAWriterWaitsOnTheRowAsync();
        await dropContext.CommitAsync(CancellationToken.None);
        var taken = await take;

        Assert.Equal(BusinessErrorMessage.NoAvailableSpots, taken.Error?.Message);
        await using var verify = NewContext("verify");
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Empty(order.AssignedEmployees);
        Assert.Equal(
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.New],
            order.OrderStatusHistory.OrderBy(s => s.Sequence).Select(s => s.Status));
    }

    /// <summary>
    /// The take's commit holds the row; the drop arrives and waits for it, then reads the crew the take left.
    /// One cleaner is still on the job, so the order stays Confirmed with the new cleaner on it, and the
    /// administrators hear of a short crew, never an empty one.
    /// </summary>
    [Fact]
    public async Task A_Drop_Waiting_On_A_Take_Counts_The_New_Cleaner_And_Keeps_The_Order_Confirmed()
    {
        await ResetAndSeedAsync(crew: [CleanerA]);

        await using var takeContext = NewContext(UserOf(CleanerB));
        await using (var takeTransaction = await takeContext.Database.BeginTransactionAsync())
        {
            var taken = await TakeHandler(takeContext, CleanerB).Handle(
                new TakeOrder.Command(OrderId, "work-contract-text"), CancellationToken.None);
            Assert.True(taken.IsSuccess, taken.Error?.Message);

            var drop = DropAndCommitAsync(CleanerA);
            await WaitUntilAWriterWaitsOnTheRowAsync();
            await takeTransaction.CommitAsync();
            var dropped = await drop;

            Assert.True(dropped.IsSuccess, dropped.Error?.Message);
            Assert.Equal(1, dropped.Value!.CrewRemaining);
        }

        await using var verify = NewContext("verify");
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.Confirmed, order.CurrentStatus);
        Assert.Equal(CleanerB, Assert.Single(order.AssignedEmployees).EmployeeId);
        Assert.Equal(AdminNotificationEventCatalog.OrderCrewShort, Assert.Single(_raised).Key);
    }

    /// <summary>
    /// The take read the order — as its validator's tracked load does — before a drop emptied the crew and
    /// committed. The take's handler then works from that stale read, and its commit is refused by the
    /// status it read.
    /// </summary>
    [Fact]
    public async Task A_Take_That_Read_Before_A_Drop_Committed_Is_Refused()
    {
        await ResetAndSeedAsync(crew: [CleanerA]);

        await using var takeContext = NewContext(UserOf(CleanerB));
        var read = await new OrderRepository(takeContext).GetQueryable()
            .Include(o => o.AssignedEmployees)
            .FirstAsync(o => o.Id == OrderId);
        Assert.Equal(OrderStatus.Confirmed, read.CurrentStatus);

        var dropped = await DropAndCommitAsync(CleanerA);
        Assert.True(dropped.IsSuccess, dropped.Error?.Message);
        var taken = await TakeHandler(takeContext, CleanerB).Handle(
            new TakeOrder.Command(OrderId, "work-contract-text"), CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.NoAvailableSpots, taken.Error?.Message);
        await using var verify = NewContext("verify");
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Empty(order.AssignedEmployees);
    }

    /// <summary>
    /// Both cleaners of a two-seat job drop at the same moment. The second waits for the first and reads the
    /// crew it left, so the last one out walks the order back to New: the order never stays Confirmed with
    /// nobody on it, and the administrators hear of the short crew and then of the empty one.
    /// </summary>
    [Fact]
    public async Task Two_Drops_At_Once_Leave_The_Order_New_With_Nobody_On_It()
    {
        await ResetAndSeedAsync(crew: [CleanerA, CleanerB]);

        await using var firstContext = NewContext(UserOf(CleanerA));
        var first = await DropHandler(firstContext, CleanerA).Handle(new DropOrder.Command(OrderId), CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(1, first.Value!.CrewRemaining);

        var second = DropAndCommitAsync(CleanerB);
        await WaitUntilAWriterWaitsOnTheRowAsync();
        await firstContext.CommitAsync(CancellationToken.None);
        var secondDropped = await second;

        Assert.True(secondDropped.IsSuccess, secondDropped.Error?.Message);
        Assert.Equal(0, secondDropped.Value!.CrewRemaining);
        await using var verify = NewContext("verify");
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Empty(order.AssignedEmployees);
        Assert.Equal(
            [AdminNotificationEventCatalog.OrderCrewShort, AdminNotificationEventCatalog.OrderCrewLost],
            _raised.Select(e => e.Key));
    }

    private async Task<BusinessResult<DropOrder.Response>> DropAndCommitAsync(string employeeId)
    {
        await using var context = NewContext(UserOf(employeeId));
        var dropped = await DropHandler(context, employeeId).Handle(new DropOrder.Command(OrderId), CancellationToken.None);
        if (dropped.IsSuccess)
        {
            await context.CommitAsync(CancellationToken.None);
        }

        return dropped;
    }

    /// <summary>The other writer has reached the order's row and is parked on its lock.</summary>
    private async Task WaitUntilAWriterWaitsOnTheRowAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var waiting = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'",
                conn);
            if ((long)(await waiting.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("No writer came to wait on the order's row.");
    }

    private static string UserOf(string employeeId) => $"{employeeId}-user";

    private CleansiaDbContext NewContext(string userId) =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(Fixture.GetConnectionString())
                .Options,
            new TestUserSessionProvider(userId, $"{userId}@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private DropOrder.Handler DropHandler(CleansiaDbContext context, string callerEmployeeId)
    {
        var admin = new Mock<IAdminNotifier>();
        admin.Setup(n => n.NotifyAsync(It.IsAny<AdminEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AdminEvent, CancellationToken>((e, _) => { lock (_raised) { _raised.Add(e); } })
            .Returns(Task.CompletedTask);
        return new DropOrder.Handler(
            new OrderRepository(context),
            CallerIs(callerEmployeeId),
            new EmployeeRepository(context),
            Mock.Of<INotificationProducer>(),
            new EmployeeActionAuditRepository(context, Mock.Of<IServiceScopeFactory>()),
            admin.Object,
            new OutboxPendingDispatch(context));
    }

    private static TakeOrder.Handler TakeHandler(CleansiaDbContext context, string callerEmployeeId) => new(
        new OrderRepository(context),
        new EmployeeRepository(context),
        CallerIs(callerEmployeeId),
        Mock.Of<INotificationProducer>(),
        Mock.Of<IEmailService>(),
        new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(context)),
        Mock.Of<IWorkContractAcceptor>(),
        NullLogger<TakeOrder.Handler>.Instance);

    private static IOrderAccessService CallerIs(string employeeId)
    {
        var access = new Mock<IOrderAccessService>();
        access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return access.Object;
    }

    private static Task<Order> LoadAsync(CleansiaDbContext context) =>
        context.Orders.IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
            .AsSplitQuery()
            .SingleAsync(o => o.Id == OrderId);

    /// <summary>A two-seat card job, paid, booked from an account, with <paramref name="crew"/> on it.</summary>
    private async Task ResetAndSeedAsync(string[] crew)
    {
        await using (var conn = new NpgsqlConnection(Fixture.GetConnectionString()))
        {
            await conn.OpenAsync();
            var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToExclude = ["pg_catalog", "information_schema"]
            });
            await respawner.ResetAsync(conn);
            await SeedTenantRegistryAsync(conn);
        }

        await using var context = NewContext("seed");
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var customer = User.CreateWithPassword("drop-take@cleansia.test", "Seed-Password-123", "Jana", "Nováková");
        customer.Id = CustomerId;
        context.Users.Add(customer);

        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "drop-take@cleansia.test",
            customerPhone: "+420777111555",
            customerAddress: Address.Create("Testovaci 14", "Praha", "11000", CountryId),
            rooms: 3,
            bathrooms: 2,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 2400m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetMaxEmployees(2);
        var stamp = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed })
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created("seed", stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddHours(1);
        }

        foreach (var employeeId in new[] { CleanerA, CleanerB })
        {
            var cleanerUser = User.CreateWithPassword(
                $"{employeeId}@cleansia.test", "Seed-Password-123", "Emp", "Loyee", UserProfile.Employee);
            cleanerUser.Id = UserOf(employeeId);
            var cleaner = Employee.CreateWithUser(cleanerUser);
            cleaner.Id = employeeId;
            cleaner.UpdateContractStatus(ContractStatus.Approved);
            cleaner.AssignWorkCountry(CountryId);
            context.Employees.Add(cleaner);
            if (crew.Contains(employeeId))
            {
                order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
            }
        }

        context.Orders.Add(order);
        await context.CommitAsync(CancellationToken.None);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
