using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
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
/// A cleaner drops a two-seat order while an administrator confirms the lockout on it. The lockout pays
/// every seat on the crew it loaded, and the pay re-reads the crew when it runs, so a seat deleted after
/// the confirmation is refused its reward. The drop locks the order's row before it reads the crew, so the
/// two are arbitrated where the drop reads: a lockout committed first gets the drop refused with
/// <c>order.not_confirmed</c>, and a drop holding the row makes the lockout's commit wait for it and then
/// re-check the order's status against what the drop left. Real Postgres, two connections, two commits. A
/// drop of the last seat also stages the customer's notices; a refused drop stages none.
/// </summary>
[Collection("PostgresCollection")]
public class DropOrderLockoutRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string AdminId = "admin-drop-lockout";
    private const string CustomerId = "user-drop-lockout-cust";
    private const string CleanerA = "emp-drop-lockout-a";
    private const string CleanerB = "emp-drop-lockout-b";
    private const string OrderId = "order-drop-lockout";
    private const string CurrencyId = "currency-czk-drop-lockout";
    private const string CountryId = "country-cz-drop-lockout";

    [Fact]
    public async Task A_Lockout_Committed_First_Refuses_The_Drop_And_The_Seat_Keeps_Its_Reward()
    {
        await ResetAndSeedAsync();

        await using (var lockoutContext = NewContext(AdminId))
        {
            var lockout = await LockoutHandler(lockoutContext)
                .Handle(new AdminCancelOrderAsLockout.Command(OrderId), CancellationToken.None);
            Assert.True(lockout.IsSuccess, lockout.Error?.Message);
            await lockoutContext.CommitAsync(CancellationToken.None);
        }

        await using var dropContext = NewContext(UserOf(CleanerA));
        var dropped = await DropHandler(dropContext, CleanerA)
            .Handle(new DropOrder.Command(OrderId), CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.OrderNotConfirmed, dropped.Error?.Message);
        await using var verify = NewContext(AdminId);
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal([CleanerA, CleanerB], order.AssignedEmployees.Select(e => e.EmployeeId).Order());
        Assert.Equal(
            [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.Cancelled],
            order.OrderStatusHistory.OrderBy(s => s.Sequence).ThenBy(s => s.CreatedOn).Select(s => s.Status));
        Assert.False(await verify.EmployeeActionAudits.IgnoreQueryFilters()
            .AnyAsync(a => a.EmployeeId == CleanerA && a.Action == EmployeeAuditAction.OrderDropped));
        Assert.DoesNotContain(BusinessErrorMessage.EmployeeNotAssigned, await PayRefusalsAsync(verify, CleanerA));
    }

    /// <summary>
    /// The drop reached the row first. The lockout read the crew before the drop committed and its commit
    /// waits on the row; the drop leaves the order Confirmed with one cleaner, so the lockout then commits,
    /// and the seat that left before it is not paid.
    /// </summary>
    [Fact]
    public async Task A_Drop_Holding_The_Row_Makes_The_Lockout_Commit_After_It_And_Leaves_The_Dropped_Seat_Unpaid()
    {
        await ResetAndSeedAsync();

        await using var dropContext = NewContext(UserOf(CleanerA));
        var dropped = await DropHandler(dropContext, CleanerA)
            .Handle(new DropOrder.Command(OrderId), CancellationToken.None);
        Assert.True(dropped.IsSuccess, dropped.Error?.Message);
        Assert.Equal(1, dropped.Value!.CrewRemaining);

        await using var lockoutContext = NewContext(AdminId);
        var lockout = await LockoutHandler(lockoutContext)
            .Handle(new AdminCancelOrderAsLockout.Command(OrderId), CancellationToken.None);
        Assert.True(lockout.IsSuccess, lockout.Error?.Message);
        var lockoutCommit = lockoutContext.CommitAsync(CancellationToken.None);
        await WaitUntilAWriterWaitsOnTheRowAsync();
        await dropContext.CommitAsync(CancellationToken.None);
        await lockoutCommit;

        await using var verify = NewContext(AdminId);
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CleanerB, Assert.Single(order.AssignedEmployees).EmployeeId);
        Assert.Contains(BusinessErrorMessage.EmployeeNotAssigned, await PayRefusalsAsync(verify, CleanerA));
        Assert.DoesNotContain(BusinessErrorMessage.EmployeeNotAssigned, await PayRefusalsAsync(verify, CleanerB));
    }

    /// <summary>
    /// The other ordering needs nothing: the drop lands while the order is still Confirmed, the lockout
    /// still finds it Confirmed and commits, and the seat that left before the confirmation is not paid.
    /// </summary>
    [Fact]
    public async Task A_Drop_Committed_Before_The_Lockout_Leaves_The_Dropped_Seat_Unpaid()
    {
        await ResetAndSeedAsync();

        await using var lockoutContext = NewContext(AdminId);
        var lockout = await LockoutHandler(lockoutContext)
            .Handle(new AdminCancelOrderAsLockout.Command(OrderId), CancellationToken.None);
        Assert.True(lockout.IsSuccess, lockout.Error?.Message);

        await using (var dropContext = NewContext(UserOf(CleanerA)))
        {
            var dropped = await DropHandler(dropContext, CleanerA)
                .Handle(new DropOrder.Command(OrderId), CancellationToken.None);
            Assert.True(dropped.IsSuccess, dropped.Error?.Message);
            await dropContext.CommitAsync(CancellationToken.None);
        }

        await lockoutContext.CommitAsync(CancellationToken.None);

        await using var verify = NewContext(AdminId);
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CleanerB, Assert.Single(order.AssignedEmployees).EmployeeId);
        Assert.Contains(BusinessErrorMessage.EmployeeNotAssigned, await PayRefusalsAsync(verify, CleanerA));
        Assert.DoesNotContain(BusinessErrorMessage.EmployeeNotAssigned, await PayRefusalsAsync(verify, CleanerB));
    }

    /// <summary>
    /// The last seat, with the lockout committed first: the drop is refused before it changes anything, so
    /// nothing is staged for the customer and the cleaner stays on the cancelled order.
    /// </summary>
    [Fact]
    public async Task A_Lockout_Committed_First_Refuses_The_Drop_Of_The_Last_Seat_And_Tells_The_Customer_Nothing()
    {
        await ResetAndSeedAsync(crew: [CleanerA]);

        await using (var lockoutContext = NewContext(AdminId))
        {
            var lockout = await LockoutHandler(lockoutContext)
                .Handle(new AdminCancelOrderAsLockout.Command(OrderId), CancellationToken.None);
            Assert.True(lockout.IsSuccess, lockout.Error?.Message);
            await lockoutContext.CommitAsync(CancellationToken.None);
        }

        await using var dropContext = NewContext(UserOf(CleanerA));
        var dropped = await DropHandler(dropContext, CleanerA, RealNotificationProducer(dropContext))
            .Handle(new DropOrder.Command(OrderId), CancellationToken.None);

        Assert.Equal(BusinessErrorMessage.OrderNotConfirmed, dropped.Error?.Message);
        Assert.Empty(dropContext.ChangeTracker.Entries<OutboxMessage>());
        Assert.Empty(dropContext.ChangeTracker.Entries<UserNotification>());
        await using var verify = NewContext(AdminId);
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
        Assert.Equal(CleanerA, Assert.Single(order.AssignedEmployees).EmployeeId);
        Assert.False(await verify.Set<UserNotification>().IgnoreQueryFilters()
            .AnyAsync(row => row.UserId == CustomerId && row.EventKey == NotificationEventCatalog.OrderCleanerReleased));
    }

    /// <summary>
    /// The last seat, with the drop holding the row: the drop walks the order back to New and tells the
    /// customer, and the lockout that read a Confirmed order before it is refused at its commit, since there
    /// is nobody left on the order to have been locked out.
    /// </summary>
    [Fact]
    public async Task A_Drop_Of_The_Last_Seat_Holding_The_Row_Refuses_The_Lockout_At_Its_Commit()
    {
        await ResetAndSeedAsync(crew: [CleanerA]);

        await using var dropContext = NewContext(UserOf(CleanerA));
        var dropped = await DropHandler(dropContext, CleanerA, RealNotificationProducer(dropContext))
            .Handle(new DropOrder.Command(OrderId), CancellationToken.None);
        Assert.True(dropped.IsSuccess, dropped.Error?.Message);
        Assert.Equal(0, dropped.Value!.CrewRemaining);

        await using var lockoutContext = NewContext(AdminId);
        var lockout = await LockoutHandler(lockoutContext)
            .Handle(new AdminCancelOrderAsLockout.Command(OrderId), CancellationToken.None);
        Assert.True(lockout.IsSuccess, lockout.Error?.Message);
        var lockoutCommit = lockoutContext.CommitAsync(CancellationToken.None);
        await WaitUntilAWriterWaitsOnTheRowAsync();
        await dropContext.CommitAsync(CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => lockoutCommit);
        await using var verify = NewContext(AdminId);
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.New, order.CurrentStatus);
        Assert.Empty(order.AssignedEmployees);
        Assert.True(await verify.Set<UserNotification>().IgnoreQueryFilters()
            .AnyAsync(row => row.UserId == CustomerId && row.EventKey == NotificationEventCatalog.OrderCleanerReleased));
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

    private static NotificationProducer RealNotificationProducer(CleansiaDbContext context) => new(
        new UserNotificationRepository(context),
        new OutboxPendingDispatch(context),
        new UserRepository(context),
        NullLogger<NotificationProducer>.Instance);

    private CleansiaDbContext NewContext(string userId) =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(Fixture.GetConnectionString())
                .Options,
            new TestUserSessionProvider(userId, $"{userId}@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private static DropOrder.Handler DropHandler(
        CleansiaDbContext context, string callerEmployeeId, INotificationProducer? notificationProducer = null)
    {
        var access = new Mock<IOrderAccessService>();
        access.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(callerEmployeeId);
        return new DropOrder.Handler(
            new OrderRepository(context),
            access.Object,
            new EmployeeRepository(context),
            notificationProducer ?? Mock.Of<INotificationProducer>(),
            new EmployeeActionAuditRepository(context, Mock.Of<IServiceScopeFactory>()),
            Mock.Of<IAdminNotifier>(),
            new OutboxPendingDispatch(context));
    }

    private static AdminCancelOrderAsLockout.Handler LockoutHandler(CleansiaDbContext context) => new(
        new OrderRepository(context),
        new ReceivableRepository(context),
        new CreditAccountRepository(context),
        new TestUserSessionProvider(AdminId, $"{AdminId}@cleansia.test"),
        Mock.Of<INotificationProducer>(),
        Mock.Of<ILiveActivityProducer>(),
        Mock.Of<ILoyaltyService>(),
        new GuestOrderAccessTokenIssuer(new GuestOrderAccessTokenRepository(context)),
        new OutboxPendingDispatch(context),
        new AuditContext(),
        TimeProvider.System);

    private static Task<Order> LoadAsync(CleansiaDbContext context) =>
        context.Orders.IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.AssignedEmployees)
            .AsSplitQuery()
            .SingleAsync(o => o.Id == OrderId);

    /// <summary>What the queued pay of <paramref name="employeeId"/> would be refused with when it runs.</summary>
    private static async Task<IReadOnlyList<string>> PayRefusalsAsync(CleansiaDbContext context, string employeeId)
    {
        var validation = await new CalculateOrderPay.Validator(
                new OrderRepository(context),
                new EmployeeRepository(context),
                new PayPeriodRepository(context),
                new EmployeePayConfigRepository(context),
                new OrderEmployeePayRepository(context),
                new ReceivableRepository(context),
                new RefundRepository(context),
                new CreditAccountRepository(context))
            .ValidateAsync(new CalculateOrderPay.Command(OrderId, employeeId));
        return validation.Errors.Select(e => e.ErrorMessage).ToList();
    }

    private async Task ResetAndSeedAsync(string[]? crew = null)
    {
        var assigned = crew ?? new[] { CleanerA, CleanerB };
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

        var customer = User.CreateWithPassword("drop-lockout@cleansia.test", "Seed-Password-123", "Jana", "Nováková");
        customer.Id = CustomerId;
        context.Users.Add(customer);

        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "drop-lockout@cleansia.test",
            customerPhone: "+420777111444",
            customerAddress: Address.Create("Testovaci 12", "Praha", "11000", CountryId),
            rooms: 3,
            bathrooms: 2,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(-20),
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
            if (assigned.Contains(employeeId))
            {
                order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
            }
        }

        order.ReportLockout(CleanerA, "2", DateTime.UtcNow.AddMinutes(-5));
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
