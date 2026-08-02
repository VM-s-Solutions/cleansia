using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// The digest sweep reads its candidates tenant-IGNORING (it is platform-wide) and runs on a timer with
/// no tenant context. Stamping the watermark through the tenant-SCOPED read therefore resolved nothing
/// for a cleaner whose <c>TenantId</c> is not null: the method returned silently AFTER the push was
/// enqueued, so the watermark never moved and the same cleaner was re-notified about the same jobs on
/// every sweep, forever (T-0529). These run the real service against a real <see cref="CleansiaDbContext"/>
/// over SQLite with a real <see cref="EmployeeRepository"/> and a real unit of work, so the watermark is
/// asserted where it matters — persisted — for a tenanted cleaner AND a single-tenant one.
/// </summary>
public sealed class NewJobsDigestTenantWatermarkTests : IDisposable
{
    private const string CountryId = "country-digest-cz";
    private const string EmployeeId = "emp-digest-tenant";
    private const string UserId = "user-digest-tenant";

    private readonly SqliteConnection _connection;

    public NewJobsDigestTenantWatermarkTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Theory]
    [InlineData(null)]                  // single-tenant mode — behaviour must be unchanged
    [InlineData("tenant-digest-1")]     // the tenanted cleaner the sweep could never stamp
    public async Task Sweep_Advances_The_Watermark_And_Does_Not_Re_Notify_On_The_Next_Sweep(string? tenantId)
    {
        await SeedEligibleCleanerWithOneFreshJobAsync(tenantId);

        var producer = new Mock<INotificationProducer>();
        producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var beforeFirstSweep = DateTimeOffset.UtcNow;
        await RunSweepAsync(producer.Object);

        producer.Verify(
            p => p.NotifyAsync(
                UserId, It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                tenantId, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var watermark = await ReadWatermarkAsync();
        Assert.NotNull(watermark);
        Assert.True(
            watermark >= beforeFirstSweep,
            $"watermark {watermark:O} did not advance to the sweep-start instant (>= {beforeFirstSweep:O})");

        // Second sweep, no new orders: the advanced watermark must now filter the same job out.
        await RunSweepAsync(producer.Object);

        producer.Verify(
            p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>Runs the sweep the way the timer does — a context with NO tenant claim.</summary>
    private async Task RunSweepAsync(INotificationProducer producer)
    {
        await using var ctx = NewContext(tenantId: null);

        var realOrderRepository = new OrderRepository(ctx);
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(() => realOrderRepository.GetQueryableIgnoringTenant());
        orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var digest = new NewJobsDigestService(
            new EmployeeRepository(ctx),
            orderRepository.Object,
            new UserNotificationPreferencesRepository(ctx),
            producer,
            ctx,
            NullLogger<NewJobsDigestService>.Instance);

        await digest.SendDigestsAsync(CancellationToken.None);
    }

    private async Task<DateTimeOffset?> ReadWatermarkAsync()
    {
        await using var ctx = NewContext(tenantId: null);
        var employee = await ctx.Set<Employee>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(e => e.Id == EmployeeId);
        return employee.LastNewJobsDigestAt;
    }

    private async Task SeedEligibleCleanerWithOneFreshJobAsync(string? tenantId)
    {
        await using (var schema = NewContext(tenantId: null))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using (var seed = NewContext(tenantId))
        {
            var user = User.CreateWithPassword(
                "digest.cleaner@cleansia.test", "Test-password-1!", "Tara", "Tenant", UserProfile.Employee);
            user.Id = UserId;
            user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

            var cleaner = Employee.CreateWithUser(user);
            cleaner.Id = EmployeeId;
            cleaner.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
            cleaner.Approve(approvedByUserId: "admin-digest");
            cleaner.AssignWorkCountry(CountryId);
            seed.Add(cleaner);

            var order = Order.Create(
                customerName: "Digest Customer",
                customerEmail: "digest-customer@cleansia.test",
                customerPhone: "+420777222333",
                customerAddress: Address.Create("Digest St 1", "Praha", "14000", CountryId),
                rooms: 2,
                bathrooms: 1,
                extras: new Dictionary<string, bool>(),
                cleaningDateTime: DateTime.UtcNow.AddDays(2),
                paymentType: PaymentType.Card,
                totalPrice: 1200m,
                currencyId: "czk",
                paymentStatus: PaymentStatus.Paid,
                userId: null);
            order.Id = "order-digest-fresh";
            order.Created("system", DateTimeOffset.UtcNow.AddDays(-1));
            AppendTrack(order, OrderStatus.New, DateTimeOffset.UtcNow.AddMinutes(-10));
            AppendTrack(order, OrderStatus.Confirmed, DateTimeOffset.UtcNow.AddMinutes(-5));
            seed.Add(order);

            await seed.CommitAsync(CancellationToken.None);
        }

        await using var verify = NewContext(tenantId: null);
        var stamped = await verify.Set<Employee>().IgnoreQueryFilters().FirstAsync(e => e.Id == EmployeeId);
        Assert.Equal(tenantId, stamped.TenantId);
        Assert.Null(stamped.LastNewJobsDigestAt);
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private CleansiaDbContext NewContext(string? tenantId) =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
