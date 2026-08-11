using System.Reflection;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
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
/// The digest's watermark is one instant per cleaner, but the overlap filter is a PER-CLEANER,
/// NON-MONOTONE rule: an order the cleaner was busy for can become takeable again when a commitment of
/// theirs is cancelled or completed, and that event writes nothing on the order itself. Advancing the
/// watermark past a skipped candidate therefore burned it permanently — it happened on every sweep where
/// the cleaner was free for even one job (T-0528).
///
/// These run the real service against a real <see cref="CleansiaDbContext"/> over SQLite with the real
/// <see cref="OrderRepository"/> answering the overlap question, so the conflict genuinely clears when the
/// blocking order is cancelled rather than because a mock was re-programmed.
/// </summary>
public sealed class NewJobsDigestSkippedJobRecoveryTests : IDisposable
{
    private const string CountryId = "country-digest-recovery";
    private const string EmployeeId = "emp-digest-recovery";
    private const string UserId = "user-digest-recovery";
    private const int SlotMinutes = 120;

    private static readonly DateTime ClashSlot = DateTime.UtcNow.AddDays(2);
    private static readonly DateTime FreeSlot = DateTime.UtcNow.AddDays(5);

    private readonly SqliteConnection _connection;

    public NewJobsDigestSkippedJobRecoveryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// AC2 — the ticket's whole point. Two new jobs, a live commitment clashing with one of them: the
    /// digest reports the free one, and once the clash is cancelled the previously-skipped job is
    /// notified instead of being lost forever.
    /// </summary>
    [Fact]
    public async Task A_Job_The_Cleaner_Was_Busy_For_Is_Notified_Once_The_Conflict_Clears()
    {
        await SeedAsync(
            board: [("order-clashing", ClashSlot), ("order-free", FreeSlot)],
            commitments: [("order-commitment", ClashSlot)]);

        var first = await RunSweepAsync();
        Assert.Equal("1", Assert.Single(first.Pushes)["count"]);

        var watermark = await ReadWatermarkAsync();
        Assert.NotNull(watermark);

        await CancelCommitmentAsync("order-commitment", watermark.Value.AddTicks(1));

        var second = await RunSweepAsync();
        Assert.Equal("1", Assert.Single(second.Pushes)["count"]);
    }

    /// <summary>AC5 — recovering the skipped job may not be bought by re-notifying while the clash stands.</summary>
    [Fact]
    public async Task A_Standing_Conflict_Does_Not_Re_Notify_On_The_Next_Sweep()
    {
        await SeedAsync(
            board: [("order-clashing", ClashSlot), ("order-free", FreeSlot)],
            commitments: [("order-commitment", ClashSlot)]);

        Assert.Single((await RunSweepAsync()).Pushes);
        Assert.Empty((await RunSweepAsync()).Pushes);
    }

    /// <summary>AC3 — when EVERY candidate clashes, nothing is pushed and the watermark stays put.</summary>
    [Fact]
    public async Task The_Watermark_Does_Not_Move_When_Every_Candidate_Overlaps()
    {
        await SeedAsync(
            board: [("order-clashing", ClashSlot)],
            commitments: [("order-commitment", ClashSlot)]);

        Assert.Empty((await RunSweepAsync()).Pushes);
        Assert.Null(await ReadWatermarkAsync());
    }

    /// <summary>
    /// AC4 — the muted branch stamps DELIBERATELY: re-enabling the toggle must not burst a backlog of
    /// jobs that stopped being fresh months ago. Pinned so the next person cannot delete it quietly.
    /// </summary>
    [Fact]
    public async Task A_Muted_Cleaner_Gets_No_Push_And_The_Watermark_Still_Advances()
    {
        await SeedAsync(board: [("order-free", FreeSlot)], muted: true);

        Assert.Empty((await RunSweepAsync()).Pushes);
        Assert.NotNull(await ReadWatermarkAsync());
    }

    /// <summary>
    /// AC7 — a cleaner who has never been digested used to match every offerable order in their country
    /// ever recorded, and then ran the overlap probe once per row. A job whose cleaning time has already
    /// started is not a new job, and that is what bounds the first sweep.
    /// </summary>
    [Fact]
    public async Task A_Never_Notified_Cleaner_Only_Considers_Jobs_That_Have_Not_Started_Yet()
    {
        var board = Enumerable.Range(1, 12)
            .Select(i => (Id: $"order-past-{i}", CleaningDateTime: DateTime.UtcNow.AddDays(-i)))
            .ToList();
        board.Add((Id: "order-future", CleaningDateTime: FreeSlot));

        await SeedAsync(board);

        var sweep = await RunSweepAsync();

        Assert.Equal("1", Assert.Single(sweep.Pushes)["count"]);
        Assert.Equal(1, sweep.OverlapChecks);
    }

    /// <summary>
    /// The digest names the statuses that RELEASE a slot; the overlap predicate names the ones that BLOCK
    /// one. The two live in different assemblies (the blocking set is private to the repository), so this
    /// is the artifact that goes red if they ever stop being exact complements — an eighth
    /// <see cref="OrderStatus"/>, or a member moving between them, silently makes the digest miss the
    /// event that frees a cleaner.
    /// </summary>
    [Fact]
    public void The_Digests_Slot_Releasing_Statuses_Are_Exactly_The_Overlap_Predicates_Non_Blocking_Ones()
    {
        var blocking = ReadPrivateStatusSet(typeof(OrderRepository), "SlotBlockingStatuses");
        var releasing = ReadPrivateStatusSet(typeof(NewJobsDigestService), "SlotReleasingStatuses");

        var expected = Enum.GetValues<OrderStatus>().Except(blocking).Order().ToArray();
        var actual = releasing.Order().ToArray();

        Assert.True(
            expected.SequenceEqual(actual),
            $"OrderStatus split drifted. The overlap predicate blocks on [{string.Join(", ", blocking)}], "
            + $"so the digest must treat [{string.Join(", ", expected)}] as slot-releasing, "
            + $"but it lists [{string.Join(", ", actual)}].");
    }

    private static OrderStatus[] ReadPrivateStatusSet(Type owner, string fieldName)
    {
        var field = owner.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(field is not null, $"{owner.Name}.{fieldName} is gone — the two status sets can no longer be compared.");
        return (OrderStatus[])field!.GetValue(null)!;
    }

    private async Task<SweepOutcome> RunSweepAsync()
    {
        await using var ctx = NewContext(tenantId: null);

        var pushes = new List<Dictionary<string, string>>();
        var producer = new Mock<INotificationProducer>();
        producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, _, args, _, _, _) => pushes.Add(args))
            .Returns(Task.CompletedTask);

        var overlapChecks = 0;
        var realOrderRepository = new OrderRepository(ctx);
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(() => realOrderRepository.GetQueryableIgnoringTenant());
        orderRepository
            .Setup(r => r.HasOverlappingOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, DateTime, int, CancellationToken>((employeeId, start, minutes, ct) =>
            {
                overlapChecks++;
                return realOrderRepository.HasOverlappingOrderIgnoringTenantAsync(employeeId, start, minutes, ct);
            });

        var digest = new NewJobsDigestService(
            new EmployeeRepository(ctx),
            orderRepository.Object,
            new UserNotificationPreferencesRepository(ctx),
            producer.Object,
            ctx,
            NullLogger<NewJobsDigestService>.Instance);

        await digest.SendDigestsAsync(CancellationToken.None);

        return new SweepOutcome(pushes, overlapChecks);
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

    private async Task SeedAsync(
        IReadOnlyCollection<(string Id, DateTime CleaningDateTime)> board,
        IReadOnlyCollection<(string Id, DateTime CleaningDateTime)>? commitments = null,
        bool muted = false)
    {
        await using (var schema = NewContext(tenantId: null))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext(tenantId: null);

        var user = User.CreateWithPassword(
            "recovery.cleaner@cleansia.test", "Test-password-1!", "Rita", "Recovery", UserProfile.Employee);
        user.Id = UserId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = EmployeeId;
        cleaner.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        cleaner.Approve(approvedByUserId: "admin-digest");
        cleaner.AssignWorkCountry(CountryId);
        seed.Add(cleaner);

        if (muted)
        {
            var preferences = UserNotificationPreferences.CreateDefaults(UserId);
            preferences.Set(NotificationCategory.NewJobsAvailable, false);
            preferences.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
            seed.Add(preferences);
        }

        foreach (var (id, cleaningDateTime) in board)
        {
            seed.Add(NewOfferableOrder(id, cleaningDateTime));
        }

        foreach (var (id, cleaningDateTime) in commitments ?? [])
        {
            var commitment = NewOfferableOrder(id, cleaningDateTime);
            commitment.AddAssignedEmployee(OrderEmployee.Create(commitment, cleaner));
            seed.Add(commitment);
        }

        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task CancelCommitmentAsync(string orderId, DateTimeOffset cancelledAt)
    {
        await using var ctx = NewContext(tenantId: null);
        var order = await ctx.Set<Order>()
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .FirstAsync(o => o.Id == orderId);

        AppendTrack(order, OrderStatus.Cancelled, cancelledAt);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static Order NewOfferableOrder(string orderId, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Recovery Customer",
            customerEmail: "recovery-customer@cleansia.test",
            customerPhone: "+420777444555",
            customerAddress: Address.Create("Recovery St 3", "Praha", "14000", CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid);
        order.Id = orderId;
        order.UpdateEstimatedTime(SlotMinutes);
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-1));
        AppendTrack(order, OrderStatus.New, DateTimeOffset.UtcNow.AddMinutes(-10));
        AppendTrack(order, OrderStatus.Confirmed, DateTimeOffset.UtcNow.AddMinutes(-5));
        return order;
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

    private sealed record SweepOutcome(IReadOnlyList<Dictionary<string, string>> Pushes, int OverlapChecks);

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
