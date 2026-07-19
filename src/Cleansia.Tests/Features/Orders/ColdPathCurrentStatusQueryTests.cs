using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The remaining cold-path latest-history status subqueries (new-jobs digest, StartOrder's
/// no-other-in-progress rule, GetMyServingCleaners, GDPR export) moved onto the persisted
/// <c>Orders.CurrentStatus</c> column — the same migration the hot paths took. These tests run
/// each consumer against a real <see cref="CleansiaDbContext"/> over SQLite with rows seeded
/// through the <see cref="Order.AddOrderStatus"/> seam, so they pin both the SQL translation of
/// the migrated predicates and their equivalence with the history-derived rule. The GDPR export
/// is a projection (not a filter), so it additionally pins the pre-backfill NULL-column fallback
/// to the authoritative history subquery.
/// </summary>
public sealed class ColdPathCurrentStatusQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ColdPathCurrentStatusQueryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    // ── GetMyServingCleaners: only orders whose CURRENT status is Completed surface cleaners. ──

    [Fact]
    public async Task GetMyServingCleaners_Returns_Cleaners_From_Completed_Orders_Only()
    {
        await EnsureSchemaAsync();
        const string customerId = "user-cold-customer";

        await using (var seed = NewContext())
        {
            var completedWith = NewEmployee("emp-cold-done", "user-cold-done", "done@cleansia.test", "Dana", "Done");
            var inProgressWith = NewEmployee("emp-cold-busy", "user-cold-busy", "busy@cleansia.test", "Boris", "Busy");
            seed.Add(completedWith);
            seed.Add(inProgressWith);

            var completed = NewOrder("cold-msc-completed", customerId);
            completed.AddAssignedEmployee(OrderEmployee.Create(completed, completedWith));
            AppendTracks(completed, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed);
            seed.Add(completed);

            var inProgress = NewOrder("cold-msc-inprogress", customerId);
            inProgress.AddAssignedEmployee(OrderEmployee.Create(inProgress, inProgressWith));
            AppendTracks(inProgress, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress);
            seed.Add(inProgress);

            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var handler = new GetMyServingCleaners.Handler(
            new OrderRepository(ctx),
            new TestUserSessionProvider(customerId, "cold-customer@cleansia.test"));

        var result = await handler.Handle(new GetMyServingCleaners.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var cleaner = Assert.Single(result.Value!);
        Assert.Equal("emp-cold-done", cleaner.EmployeeId);
        Assert.Equal("Dana Done", cleaner.FullName);
    }

    // ── StartOrder: the no-other-in-progress rule reads the column, same answer as the history rule. ──

    [Fact]
    public async Task StartOrder_Rejects_When_Another_Assigned_Order_Is_Currently_InProgress()
    {
        await EnsureSchemaAsync();
        var (validator, _) = await SeedStartOrderScenarioAsync(siblingLatestStatus: OrderStatus.InProgress);

        var result = await validator.ValidateAsync(new StartOrder.Command("cold-start-target"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeAlreadyHasOrderInProgress);
    }

    [Fact]
    public async Task StartOrder_Allows_When_The_Sibling_Order_Moved_Past_InProgress()
    {
        await EnsureSchemaAsync();
        var (validator, _) = await SeedStartOrderScenarioAsync(siblingLatestStatus: OrderStatus.Completed);

        var result = await validator.ValidateAsync(new StartOrder.Command("cold-start-target"));

        Assert.True(result.IsValid);
    }

    private async Task<(StartOrder.Validator Validator, string EmployeeId)> SeedStartOrderScenarioAsync(
        OrderStatus siblingLatestStatus)
    {
        const string employeeId = "emp-cold-start";

        await using (var seed = NewContext())
        {
            var employee = NewEmployee(employeeId, "user-cold-start", "start@cleansia.test", "Ela", "Start");
            employee.Approve(approvedByUserId: "admin-cold");
            seed.Add(employee);

            var target = NewOrder("cold-start-target", "user-cold-c1");
            target.AddAssignedEmployee(OrderEmployee.Create(target, employee));
            AppendTracks(target, OrderStatus.New, OrderStatus.Confirmed);
            seed.Add(target);

            var sibling = NewOrder("cold-start-sibling", "user-cold-c2");
            sibling.AddAssignedEmployee(OrderEmployee.Create(sibling, employee));
            AppendTracks(sibling, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, siblingLatestStatus);
            seed.Add(sibling);

            await seed.CommitAsync(CancellationToken.None);
        }

        var ctx = NewContext();
        var employeeRepository = new Mock<IEmployeeRepository>();
        employeeRepository
            .Setup(r => r.GetByIdAsync(employeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ValidatorTestHelpers.BuildEmployee(employeeId, ContractStatus.Approved, withAddress: true));
        var accessService = new Mock<IOrderAccessService>();
        accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(employeeId);

        var validator = new StartOrder.Validator(
            new OrderRepository(ctx),
            employeeRepository.Object,
            accessService.Object);
        return (validator, employeeId);
    }

    // ── New-jobs digest: current status must be in the available set; the watermark still
    //    reads the latest track's timestamp. ──

    [Fact]
    public async Task NewJobsDigest_Notifies_For_Fresh_Available_Orders_Only()
    {
        await EnsureSchemaAsync();
        const string countryId = "country-cold-cz";
        var watermark = DateTimeOffset.UtcNow.AddHours(-1);

        await using (var seed = NewContext())
        {
            var cleaner = NewEmployee("emp-cold-digest", "user-cold-digest", "digest@cleansia.test", "Nora", "New");
            cleaner.Approve(approvedByUserId: "admin-cold");
            cleaner.AssignWorkCountry(countryId);
            cleaner.MarkNewJobsDigestSent(watermark);
            seed.Add(cleaner);

            // Confirmed AFTER the watermark → the one takeable job in the digest.
            var fresh = NewOrder("cold-digest-fresh", userId: null, countryId: countryId);
            AppendTrack(fresh, OrderStatus.New, watermark.AddMinutes(-30));
            AppendTrack(fresh, OrderStatus.Confirmed, watermark.AddMinutes(30));
            seed.Add(fresh);

            // Completed (not in the available set) even though it flipped after the watermark.
            var completed = NewOrder("cold-digest-completed", userId: null, countryId: countryId);
            AppendTrack(completed, OrderStatus.Confirmed, watermark.AddMinutes(-30));
            AppendTrack(completed, OrderStatus.Completed, watermark.AddMinutes(35));
            seed.Add(completed);

            // Available, but its last transition predates the watermark → already digested.
            var stale = NewOrder("cold-digest-stale", userId: null, countryId: countryId);
            AppendTrack(stale, OrderStatus.Confirmed, watermark.AddHours(-2));
            seed.Add(stale);

            await seed.CommitAsync(CancellationToken.None);
        }

        await using var ctx = NewContext();
        var realOrderRepository = new OrderRepository(ctx);
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(() => realOrderRepository.GetQueryableIgnoringTenant());
        orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var producer = new Mock<INotificationProducer>();
        (string UserId, Dictionary<string, string> Args)? notified = null;
        producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (userId, _, args, _, _, _) => notified = (userId, args))
            .Returns(Task.CompletedTask);

        var digest = new NewJobsDigestService(
            new EmployeeRepository(ctx),
            orderRepository.Object,
            new UserNotificationPreferencesRepository(ctx),
            producer.Object,
            Mock.Of<IUnitOfWork>(),
            NullLogger<NewJobsDigestService>.Instance);

        await digest.SendDigestsAsync(CancellationToken.None);

        producer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(notified);
        Assert.Equal("user-cold-digest", notified!.Value.UserId);
        Assert.Equal("1", notified.Value.Args["count"]);
    }

    // ── GDPR export: a projection, so a pre-backfill NULL column must still export the true
    //    status via the history fallback. ──

    [Fact]
    public async Task GdprExport_Order_Status_Matches_History_Including_The_Null_Column_Fallback()
    {
        await EnsureSchemaAsync();
        const string userId = "user-cold-gdpr";

        await using (var seed = NewContext())
        {
            var user = User.CreateWithPassword("gdpr-cold@cleansia.test", "Test-password-1!", "Gita", "Gdpr", UserProfile.Customer);
            user.Id = userId;
            user.Created("system", DateTimeOffset.UtcNow.AddDays(-30));
            seed.Add(user);

            var current = NewOrder("cold-gdpr-current", userId);
            AppendTracks(current, OrderStatus.New, OrderStatus.Confirmed, OrderStatus.InProgress, OrderStatus.Completed);
            seed.Add(current);

            var legacy = NewOrder("cold-gdpr-legacy", userId);
            AppendTracks(legacy, OrderStatus.New, OrderStatus.Confirmed);
            seed.Add(legacy);

            await seed.CommitAsync(CancellationToken.None);
        }

        await using (var nullCtx = NewContext())
        {
            await nullCtx.Database.ExecuteSqlRawAsync(
                "UPDATE \"Orders\" SET \"CurrentStatus\" = NULL WHERE \"Id\" = {0}", "cold-gdpr-legacy");
        }

        await using var ctx = NewContext();
        var consentRepository = new Mock<IUserConsentRepository>();
        consentRepository
            .Setup(r => r.GetByUserIdNoTrackingAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new GdprExportService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            Mock.Of<IEmployeeDocumentRepository>(),
            Mock.Of<IEmployeeInvoiceRepository>(),
            consentRepository.Object);

        var export = await service.BuildAsync(userId, exportedBy: "admin-cold", CancellationToken.None);

        Assert.Equal(2, export.Orders.Count);
        Assert.Equal(OrderStatus.Completed, export.Orders.Single(o => o.Id == "cold-gdpr-current").Status);
        Assert.Equal(OrderStatus.Confirmed, export.Orders.Single(o => o.Id == "cold-gdpr-legacy").Status);
    }

    private static Order NewOrder(string orderId, string? userId, string countryId = "cz")
    {
        var order = Order.Create(
            customerName: "Cold Path Customer",
            customerEmail: "cold-customer@cleansia.test",
            customerPhone: "+420777222333",
            customerAddress: Address.Create("Cold St 1", "Praha", "14000", countryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = orderId;
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-2));
        return order;
    }

    private static Employee NewEmployee(string employeeId, string userId, string email, string first, string last)
    {
        var user = User.CreateWithPassword(email, "Test-password-1!", first, last, UserProfile.Employee);
        user.Id = userId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        return employee;
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private static void AppendTracks(Order order, params OrderStatus[] statuses)
    {
        var stamp = DateTimeOffset.UtcNow.AddHours(-6);
        foreach (var status in statuses)
        {
            AppendTrack(order, status, stamp);
            stamp = stamp.AddMinutes(20);
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
