using Cleansia.Core.AppServices.Features.Orders;
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
using Microsoft.Extensions.Logging;
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
    private const string SecondEmployeeId = "emp-digest-recovery-2";
    private const string SecondUserId = "user-digest-recovery-2";
    private const string OtherCompanyTenantId = "tenant-digest-other";
    private const int SlotMinutes = 120;

    private static readonly DateTime ClashSlot = DateTime.UtcNow.AddDays(2);
    private static readonly DateTime FreeSlot = DateTime.UtcNow.AddDays(5);

    /// <summary>
    /// Whole-minute, because SQLite does the interval arithmetic on text to the millisecond while .NET
    /// compares ticks; at a whole minute the two agree on every boundary below.
    /// </summary>
    private static readonly DateTime Base = WholeMinute(DateTime.UtcNow.AddDays(3));

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
        Assert.Equal(["order-future"], Assert.Single(sweep.CommitmentReads).CandidateIds);
    }

    /// <summary>
    /// One read of the cleaner's commitments answers every candidate: twelve fresh jobs cost a single
    /// read naming all twelve, and no read per job.
    /// </summary>
    [Fact]
    public async Task Every_Candidate_Of_A_Cleaner_Is_Answered_By_One_Commitment_Read()
    {
        var board = Enumerable.Range(0, 12)
            .Select(i => new Job($"order-fresh-{i}", Base.AddHours(i * 3)))
            .ToList();
        await SeedJobsAsync(
            board,
            commitments: [new Job("order-commitment-before", Base.AddDays(-1)), new Job("order-commitment-after", Base.AddDays(3))]);

        var sweep = await RunSweepAsync();

        Assert.Equal("12", Assert.Single(sweep.Pushes)["count"]);
        Assert.NotNull(await ReadWatermarkAsync());
        Assert.Equal(0, sweep.SingleWindowProbes);
        var read = Assert.Single(sweep.CommitmentReads);
        Assert.Equal(EmployeeId, read.EmployeeId);
        Assert.Equal(board.Select(j => j.Id).Order(), read.CandidateIds.Order());
    }

    /// <summary>
    /// The commitment holds [Base, Base + 2 h). The interval is half-open, so a job touching either end
    /// is free, while one minute of overlap, or containment either way, is a clash. Shorter and longer
    /// jobs than the commitment are judged by the same terms. The anchor job always clashes and is never
    /// counted; it is there so the commitment is read whatever the job under test asks, and the edge is
    /// decided by the overlap terms themselves rather than by what the query happened to fetch.
    /// </summary>
    [Theory]
    [InlineData(-120, 120, true)]
    [InlineData(-60, 60, true)]
    [InlineData(120, 60, true)]
    [InlineData(120, 240, true)]
    [InlineData(-119, 120, false)]
    [InlineData(119, 60, false)]
    [InlineData(30, 60, false)]
    [InlineData(-60, 240, false)]
    public async Task A_Job_Clashes_Exactly_When_Its_Window_Overlaps_The_Half_Open_Commitment(
        int offsetMinutes, int jobMinutes, bool counted)
    {
        await SeedJobsAsync(
            board:
            [
                new Job("order-candidate", Base.AddMinutes(offsetMinutes), jobMinutes),
                new Job("order-anchor", Base.AddMinutes(30), 60),
            ],
            commitments: [new Job("order-commitment", Base)]);

        var sweep = await RunSweepAsync();

        Assert.Equal(counted ? ["1"] : Array.Empty<string>(), sweep.Pushes.Select(p => p["count"]));
    }

    /// <summary>Only a live commitment holds the cleaner's time; a finished or cancelled one gives it back.</summary>
    [Theory]
    [InlineData(OrderStatus.New, false)]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.OnTheWay, false)]
    [InlineData(OrderStatus.InProgress, false)]
    [InlineData(OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Cancelled, true)]
    public async Task Only_A_Live_Commitment_Keeps_A_Clashing_Job_Out_Of_The_Count(
        OrderStatus commitmentStatus, bool counted)
    {
        await SeedJobsAsync(
            board: [new Job("order-candidate", Base.AddMinutes(30), 60)],
            commitments: [new Job("order-commitment", Base, Status: commitmentStatus)]);

        var sweep = await RunSweepAsync();

        Assert.Equal(counted ? ["1"] : Array.Empty<string>(), sweep.Pushes.Select(p => p["count"]));
    }

    /// <summary>
    /// The cleaner works for the default company and holds a job booked through another one. It is one
    /// calendar, so that commitment still keeps the clashing job out; the free job is the control.
    /// </summary>
    [Fact]
    public async Task A_Commitment_Booked_Through_Another_Company_Still_Clashes()
    {
        await SeedJobsAsync(
            board: [new Job("order-clashing", Base.AddMinutes(30), 60), new Job("order-free", Base.AddDays(2))],
            commitments: [new Job("order-commitment", Base, TenantId: OtherCompanyTenantId)]);

        await using (var verify = NewContext(tenantId: TestTenants.Default))
        {
            var commitment = await verify.Set<Order>().IgnoreQueryFilters().FirstAsync(o => o.Id == "order-commitment");
            Assert.Equal(OtherCompanyTenantId, commitment.TenantId);
        }

        Assert.Equal("1", Assert.Single((await RunSweepAsync()).Pushes)["count"]);
    }

    /// <summary>
    /// A row longer than <see cref="Order.MaxOrderSpanHours"/> is outside the overlap guarantee: each job
    /// only sees commitments starting on or after its own scan floor. This one starts a minute before the
    /// later job's floor and runs through both jobs, so it blocks the earlier job and not the later one,
    /// which is the answer each job gets when asked about on its own.
    /// </summary>
    [Fact]
    public async Task A_Row_Longer_Than_The_Span_Bound_Blocks_Only_The_Jobs_Whose_Floor_It_Starts_On()
    {
        var later = Base.AddHours(100);
        await SeedJobsAsync(
            board: [new Job("order-earlier", Base), new Job("order-later", later)],
            commitments:
            [
                new Job("order-malformed", later.AddHours(-Order.MaxOrderSpanHours).AddMinutes(-1), Minutes: 200 * 60),
            ]);

        Assert.Equal("1", Assert.Single((await RunSweepAsync()).Pushes)["count"]);
    }

    /// <summary>
    /// The clash is decided before the mute is read, so a muted cleaner whose every job clashes has
    /// nothing new: no push, and no watermark that would burn the clashing jobs.
    /// </summary>
    [Fact]
    public async Task A_Muted_Cleaner_Whose_Every_Job_Clashes_Keeps_Their_Watermark()
    {
        await SeedAsync(
            board: [("order-clashing", ClashSlot)],
            commitments: [("order-commitment", ClashSlot)],
            muted: true);

        Assert.Empty((await RunSweepAsync()).Pushes);
        Assert.Null(await ReadWatermarkAsync());
    }

    /// <summary>
    /// A commitment read that fails costs that cleaner this sweep and nobody else: they are not pushed,
    /// their watermark does not move, and the next cleaner is still told.
    /// </summary>
    [Fact]
    public async Task A_Failed_Commitment_Read_Skips_Only_That_Cleaner()
    {
        await SeedAsync(board: [("order-free", FreeSlot)]);
        await SeedSecondCleanerAsync();

        var sweep = await RunSweepAsync(failCommitmentReadFor: EmployeeId);

        var failure = Assert.Single(sweep.Failures);
        Assert.Contains($"cleaner {EmployeeId};", failure.Message);
        Assert.Equal("commitment read failed", failure.Exception!.Message);

        var push = Assert.Single(sweep.PushesByUser);
        Assert.Equal(SecondUserId, push.UserId);
        Assert.Equal("1", push.Args["count"]);
        Assert.Null(await ReadWatermarkAsync(EmployeeId));
        Assert.NotNull(await ReadWatermarkAsync(SecondEmployeeId));
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

    private async Task<SweepOutcome> RunSweepAsync(string? failCommitmentReadFor = null)
    {
        await using var ctx = NewContext(tenantId: TestTenants.Default);

        var pushes = new List<(string UserId, Dictionary<string, string> Args)>();
        var producer = new Mock<INotificationProducer>();
        producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (userId, _, args, _, _, _) => pushes.Add((userId, args)))
            .Returns(Task.CompletedTask);

        // Strict, so a member nobody set up throws instead of answering null. The single-window probe is
        // still forwarded and counted, so probing per candidate shows up as a count, not as a failure.
        var singleWindowProbes = 0;
        var realOrderRepository = new OrderRepository(ctx);
        var orderRepository = new Mock<IOrderRepository>(MockBehavior.Strict);
        orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(() => realOrderRepository.GetQueryableIgnoringTenant());
        orderRepository
            .Setup(r => r.HasOverlappingOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, DateTime, int, CancellationToken>((employeeId, start, minutes, ct) =>
            {
                singleWindowProbes++;
                return employeeId == failCommitmentReadFor
                    ? Task.FromException<bool>(new InvalidOperationException("commitment read failed"))
                    : realOrderRepository.HasOverlappingOrderIgnoringTenantAsync(employeeId, start, minutes, ct);
            });

        var commitmentLookups = new List<(string EmployeeId, IReadOnlyList<string> CandidateIds)>();
        orderRepository
            .Setup(r => r.GetOverlappedCandidateIdsIgnoringTenantAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<(string Id, DateTime CleaningDateTime, int EstimatedTimeMinutes)>>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, IReadOnlyCollection<(string Id, DateTime CleaningDateTime, int EstimatedTimeMinutes)>, CancellationToken>(
                (employeeId, candidates, ct) =>
                {
                    commitmentLookups.Add((employeeId, [.. candidates.Select(c => c.Id)]));
                    return employeeId == failCommitmentReadFor
                        ? Task.FromException<IReadOnlySet<string>>(new InvalidOperationException("commitment read failed"))
                        : realOrderRepository.GetOverlappedCandidateIdsIgnoringTenantAsync(employeeId, candidates, ct);
                });

        var log = new List<(Exception? Exception, string Message)>();
        var digest = new NewJobsDigestService(
            new EmployeeRepository(ctx),
            orderRepository.Object,
            new UserNotificationPreferencesRepository(ctx),
            producer.Object,
            ctx,
            new CapturingLogger(log));

        await digest.SendDigestsAsync(CancellationToken.None);

        // The sweep logs and swallows a cleaner's failure, so without this a harness fault reads as
        // "nothing to send" and every assertion of absence passes for the wrong reason.
        var failures = log.Where(e => e.Exception is not null).ToList();
        if (failCommitmentReadFor is null)
        {
            Assert.True(failures.Count == 0, $"the sweep swallowed: {string.Join(" | ", failures.Select(f => f.Exception))}");
        }

        return new SweepOutcome(pushes, singleWindowProbes, commitmentLookups, failures);
    }

    private async Task<DateTimeOffset?> ReadWatermarkAsync(string employeeId = EmployeeId)
    {
        await using var ctx = NewContext(tenantId: TestTenants.Default);
        var employee = await ctx.Set<Employee>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(e => e.Id == employeeId);
        return employee.LastNewJobsDigestAt;
    }

    private Task SeedAsync(
        IReadOnlyCollection<(string Id, DateTime CleaningDateTime)> board,
        IReadOnlyCollection<(string Id, DateTime CleaningDateTime)>? commitments = null,
        bool muted = false) =>
        SeedJobsAsync(
            [.. board.Select(b => new Job(b.Id, b.CleaningDateTime))],
            [.. (commitments ?? []).Select(c => new Job(c.Id, c.CleaningDateTime))],
            muted);

    private async Task SeedJobsAsync(
        IReadOnlyCollection<Job> board,
        IReadOnlyCollection<Job>? commitments = null,
        bool muted = false)
    {
        await using (var schema = NewContext(tenantId: TestTenants.Default))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext(tenantId: TestTenants.Default);

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

        foreach (var job in board)
        {
            seed.Add(NewOfferableOrder(job.Id, job.CleaningDateTime, job.Minutes));
        }

        foreach (var job in commitments ?? [])
        {
            seed.Add(NewCommitment(job, cleaner));
        }

        await seed.CommitAsync(CancellationToken.None);
    }

    /// <summary>A second approved cleaner in the same country, so they see the same board.</summary>
    private async Task SeedSecondCleanerAsync()
    {
        await using var seed = NewContext(tenantId: TestTenants.Default);

        var user = User.CreateWithPassword(
            "recovery.cleaner.2@cleansia.test", "Test-password-1!", "Rosa", "Recovery", UserProfile.Employee);
        user.Id = SecondUserId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = SecondEmployeeId;
        cleaner.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        cleaner.Approve(approvedByUserId: "admin-digest");
        cleaner.AssignWorkCountry(CountryId);
        seed.Add(cleaner);

        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task CancelCommitmentAsync(string orderId, DateTimeOffset cancelledAt)
    {
        await using var ctx = NewContext(tenantId: TestTenants.Default);
        var order = await ctx.Set<Order>()
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .FirstAsync(o => o.Id == orderId);

        AppendTrack(order, OrderStatus.Cancelled, cancelledAt);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private static Order NewCommitment(Job job, Employee cleaner)
    {
        var order = NewOfferableOrder(job.Id, job.CleaningDateTime, job.Minutes, finalStatus: job.Status);
        if (job.TenantId is not null)
        {
            order.TenantId = job.TenantId;
        }

        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        return order;
    }

    private static Order NewOfferableOrder(
        string orderId,
        DateTime cleaningDateTime,
        int minutes = SlotMinutes,
        OrderStatus finalStatus = OrderStatus.Confirmed)
    {
        var order = Order.Create(
            customerName: "Recovery Customer",
            customerEmail: "recovery-customer@cleansia.test",
            customerPhone: "+420777444555",
            customerAddress: Address.Create("Recovery St 3", "Praha", "14000", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.UpdateEstimatedTime(minutes);
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-1));
        AppendTrack(order, OrderStatus.New, DateTimeOffset.UtcNow.AddMinutes(-10));
        if (finalStatus != OrderStatus.New)
        {
            AppendTrack(order, finalStatus, DateTimeOffset.UtcNow.AddMinutes(-5));
        }

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

    private static DateTime WholeMinute(DateTime value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMinute), DateTimeKind.Utc);

    private sealed record Job(
        string Id,
        DateTime CleaningDateTime,
        int Minutes = SlotMinutes,
        OrderStatus Status = OrderStatus.Confirmed,
        string? TenantId = null);

    /// <param name="CommitmentLookups">Every call of the batch read; one with no candidates issues no
    /// query (pinned in <c>HasOverlappingOrderStatusTests</c>), so only the others are reads.</param>
    private sealed record SweepOutcome(
        IReadOnlyList<(string UserId, Dictionary<string, string> Args)> PushesByUser,
        int SingleWindowProbes,
        IReadOnlyList<(string EmployeeId, IReadOnlyList<string> CandidateIds)> CommitmentLookups,
        IReadOnlyList<(Exception? Exception, string Message)> Failures)
    {
        public IReadOnlyList<Dictionary<string, string>> Pushes => [.. PushesByUser.Select(p => p.Args)];

        public IReadOnlyList<(string EmployeeId, IReadOnlyList<string> CandidateIds)> CommitmentReads =>
            [.. CommitmentLookups.Where(l => l.CandidateIds.Count > 0)];
    }

    private sealed class CapturingLogger(List<(Exception? Exception, string Message)> entries)
        : ILogger<NewJobsDigestService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((exception, formatter(state, exception)));
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
