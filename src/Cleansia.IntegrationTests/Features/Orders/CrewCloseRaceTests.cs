using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The crew close on real Postgres, two connections and both commit orders (owner ruling 2026-10-26: each
/// cleaner marks their part done, then any of them completes the job). Both writers lock the order's row
/// before they read it, so whichever reaches it second reads what the first committed: two cleaners
/// pressing Complete at once complete the job once, and two cleaners marking at once each see the other.
/// </summary>
[Collection("PostgresCollection")]
public class CrewCloseRaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CleanerA = "emp-crew-race-a";
    private const string CleanerB = "emp-crew-race-b";
    private const string OrderId = "order-crew-race";
    private const string CurrencyId = "currency-czk-crew-race";
    private const string CountryId = "country-cz-crew-race";
    private const string CustomerId = "user-crew-race-cust";

    private readonly List<string> _pushed = [];
    private readonly List<string> _loyaltyGrants = [];

    /// <summary>
    /// Both cleaners have marked done and both press Complete. The first holds the row; the second passed
    /// its validator on the job still in progress and waits on the row, then reads the job completed and
    /// answers so without completing it again: one Completed row, one customer push, one status e-mail, one
    /// receipt request, one points grant, and each seat's pay asked once.
    /// </summary>
    [Theory]
    [InlineData(CleanerA, CleanerB)]
    [InlineData(CleanerB, CleanerA)]
    public async Task Two_Cleaners_Completing_At_Once_Complete_The_Job_Once(string first, string second)
    {
        await ResetAndSeedAsync(markedDone: [CleanerA, CleanerB]);

        await using var firstContext = NewContext(UserOf(first));
        var validated = await CompleteValidator(firstContext, first).ValidateAsync(new CompleteOrder.Command(OrderId));
        Assert.True(validated.IsValid, string.Join("; ", validated.Errors.Select(e => e.ErrorMessage)));
        var completed = await CompleteHandler(firstContext).Handle(new CompleteOrder.Command(OrderId), CancellationToken.None);
        Assert.True(completed.IsSuccess, completed.Error?.Message);

        var other = CompleteAndCommitAsync(second);
        await UntilItWaitsOnTheRowOrEndsAsync(other);
        await firstContext.CommitAsync(CancellationToken.None);
        var otherCompleted = await other;

        Assert.True(otherCompleted.IsSuccess, otherCompleted.Error?.Message);
        Assert.Equal(OrderStatus.Completed, otherCompleted.Value!.NewStatus);
        await using var verify = NewContext("verify");
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.Completed, order.CurrentStatus);
        Assert.Single(order.OrderStatusHistory, s => s.Status == OrderStatus.Completed);
        Assert.Equal([NotificationEventCatalog.OrderCompleted], _pushed);
        Assert.Equal([OrderId], _loyaltyGrants);
        var queued = await verify.OutboxMessages.IgnoreQueryFilters().Select(m => m.QueueName).ToListAsync();
        Assert.Single(queued, q => q == QueueNames.SendEmail);
        Assert.Single(queued, q => q == QueueNames.GenerateReceipt);
        Assert.Equal(2, queued.Count(q => q == QueueNames.CalculateOrderPay));
    }

    /// <summary>
    /// The last two cleaners mark their parts done at the same moment. The second waits on the row the
    /// first holds, then reads the first one's mark, so it answers that the crew is done and the shared
    /// Complete can show; both marks are kept and the job stays in progress.
    /// </summary>
    [Theory]
    [InlineData(CleanerA, CleanerB)]
    [InlineData(CleanerB, CleanerA)]
    public async Task Two_Last_Marks_At_Once_Keep_Both_And_The_Later_One_Finds_The_Crew_Done(string first, string second)
    {
        await ResetAndSeedAsync(markedDone: []);

        await using var firstContext = NewContext(UserOf(first));
        var marked = await MarkHandler(firstContext, first).Handle(new MarkMyPartDone.Command(OrderId), CancellationToken.None);
        Assert.True(marked.IsSuccess, marked.Error?.Message);
        Assert.False(marked.Value!.CrewDone);

        var other = MarkAndCommitAsync(second);
        await UntilItWaitsOnTheRowOrEndsAsync(other);
        await firstContext.CommitAsync(CancellationToken.None);
        var otherMarked = await other;

        Assert.True(otherMarked.IsSuccess, otherMarked.Error?.Message);
        Assert.True(otherMarked.Value!.CrewDone);
        await using var verify = NewContext("verify");
        var order = await LoadAsync(verify);
        Assert.Equal(OrderStatus.InProgress, order.CurrentStatus);
        Assert.All(order.AssignedEmployees, seat => Assert.NotNull(seat.CompletionConfirmedAt));
    }

    private async Task<BusinessResult<CompleteOrder.Response>> CompleteAndCommitAsync(string employeeId)
    {
        await using var context = NewContext(UserOf(employeeId));
        var validated = await CompleteValidator(context, employeeId).ValidateAsync(new CompleteOrder.Command(OrderId));
        Assert.True(validated.IsValid, string.Join("; ", validated.Errors.Select(e => e.ErrorMessage)));
        var completed = await CompleteHandler(context).Handle(new CompleteOrder.Command(OrderId), CancellationToken.None);
        if (completed.IsSuccess)
        {
            await context.CommitAsync(CancellationToken.None);
        }

        return completed;
    }

    private async Task<BusinessResult<MarkMyPartDone.Response>> MarkAndCommitAsync(string employeeId)
    {
        await using var context = NewContext(UserOf(employeeId));
        var marked = await MarkHandler(context, employeeId).Handle(new MarkMyPartDone.Command(OrderId), CancellationToken.None);
        if (marked.IsSuccess)
        {
            await context.CommitAsync(CancellationToken.None);
        }

        return marked;
    }

    /// <summary>
    /// The other writer has reached the order's row and is parked on its lock, or has already finished,
    /// which is what a writer that takes no lock does.
    /// </summary>
    private async Task UntilItWaitsOnTheRowOrEndsAsync(Task writer)
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && !writer.IsCompleted)
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
    }

    private static string UserOf(string employeeId) => $"{employeeId}-user";

    private CleansiaDbContext NewContext(string userId) =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>()
                .UseNpgsql(Fixture.GetConnectionString())
                .Options,
            new TestUserSessionProvider(userId, $"{userId}@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    private static CompleteOrder.Validator CompleteValidator(CleansiaDbContext context, string callerEmployeeId)
    {
        var photos = new Mock<IOrderPhotoRepository>();
        photos.Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var acceptances = new Mock<IWorkContractAcceptanceRepository>();
        acceptances.Setup(r => r.AnyForSeatAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new CompleteOrder.Validator(
            new OrderRepository(context),
            new EmployeeRepository(context),
            photos.Object,
            CallerIs(callerEmployeeId),
            acceptances.Object);
    }

    private CompleteOrder.Handler CompleteHandler(CleansiaDbContext context)
    {
        var notifications = new Mock<INotificationProducer>();
        notifications
            .Setup(n => n.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, eventKey, _, _, _, _) => { lock (_pushed) { _pushed.Add(eventKey); } })
            .Returns(Task.CompletedTask);
        var loyalty = new Mock<ILoyaltyService>();
        loyalty.Setup(l => l.GrantForCompletedOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((orderId, _) => { lock (_loyaltyGrants) { _loyaltyGrants.Add(orderId); } })
            .Returns(Task.CompletedTask);
        return new CompleteOrder.Handler(
            new OrderRepository(context),
            new OutboxPendingDispatch(context),
            notifications.Object,
            Mock.Of<ILiveActivityProducer>(),
            loyalty.Object,
            Mock.Of<IReferralService>());
    }

    private static MarkMyPartDone.Handler MarkHandler(CleansiaDbContext context, string callerEmployeeId) =>
        new(new OrderRepository(context), CallerIs(callerEmployeeId));

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

    /// <summary>A two-seat card job in progress, paid, booked from an account, with both cleaners on it.</summary>
    private async Task ResetAndSeedAsync(string[] markedDone)
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

        var customer = User.CreateWithPassword("crew-race@cleansia.test", "Seed-Password-123", "Jana", "Nováková");
        customer.Id = CustomerId;
        context.Users.Add(customer);

        var order = Order.Create(
            customerName: "Jana Nováková",
            customerEmail: "crew-race@cleansia.test",
            customerPhone: "+420777111666",
            customerAddress: Address.Create("Testovaci 15", "Praha", "11000", CountryId),
            rooms: 3,
            bathrooms: 2,
            cleaningDateTime: DateTime.UtcNow.AddHours(-1),
            paymentType: PaymentType.Card,
            totalPrice: 2400m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.SetMaxEmployees(2);
        var stamp = DateTimeOffset.UtcNow.AddHours(-3);
        foreach (var status in new[] { OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress })
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
            cleaner.UpdateAddress(Address.Create("Domovska 3", "Praha", "11000", CountryId));
            context.Employees.Add(cleaner);
            var seat = OrderEmployee.Create(order, cleaner);
            if (markedDone.Contains(employeeId))
            {
                seat.MarkCompletionConfirmed(DateTime.UtcNow.AddMinutes(-5));
            }

            order.AddAssignedEmployee(seat);
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
