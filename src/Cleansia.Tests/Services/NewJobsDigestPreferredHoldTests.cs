using Cleansia.Core.AppServices.Features.Orders;
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
/// TC-PREF-DIGEST-0a/0b (ADR-0036 D5.3). The digest is the one surface that carries TWO rules about a
/// held order, and they pull in opposite directions: the shared visibility conjunct must keep it OUT of
/// the count while the hold is live, and the freshness disjunct must let it back IN once the hold
/// expires.
///
/// <para>The second half is the non-obvious one. Freshness is a single per-cleaner watermark compared
/// against the order's own status tracks, and a held order's only track is written at creation — so by
/// the time the hold lifts, its whole history is older than every other cleaner's watermark and it
/// leaves the notification channel permanently, discoverable only by someone who happens to scroll the
/// board. A 48-minute optimisation would have become a permanent loss of reach, through a back door no
/// reasonable implementer would predict.</para>
///
/// <para>0a is what makes 0b mean anything: an after-expiry test alone passes against both the correct
/// implementation and one that never withheld the order in the first place.</para>
///
/// <para>Run against a real <see cref="CleansiaDbContext"/> over SQLite with the real repositories, so
/// the disjunction is genuinely translated rather than evaluated in memory.</para>
/// </summary>
public sealed class NewJobsDigestPreferredHoldTests : IDisposable
{
    private const string CountryId = "country-digest-hold";
    private const string EmployeeId = "emp-digest-hold";
    private const string UserId = "user-digest-hold";
    private const string RivalEmployeeId = "emp-digest-hold-rival";
    private const int SlotMinutes = 120;

    private static readonly DateTime HeldSlot = DateTime.UtcNow.AddDays(3);
    private static readonly DateTime OpenSlot = DateTime.UtcNow.AddDays(6);
    private static readonly DateTime ClashSlot = DateTime.UtcNow.AddDays(9);

    private readonly SqliteConnection _connection;

    public NewJobsDigestPreferredHoldTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// The blocking pin. Two fresh offerable jobs, one held for someone else: the count is 1, not 2.
    /// The digest emits a number and never an identity, so nothing leaks here — but a number that
    /// includes a job the cleaner can neither open nor take is a lie, and it is the kind of lie that
    /// teaches cleaners to stop opening the notification.
    /// </summary>
    [Fact]
    public async Task A_Job_Held_For_Someone_Else_Is_Not_Counted_In_The_Digest()
    {
        await SeedAsync(heldForRival: true);

        var pushes = await RunSweepAsync();

        Assert.Equal("1", Assert.Single(pushes)["count"]);
    }

    /// <summary>
    /// The control: the SAME fixture with no hold on it counts both. Without this, the assertion above
    /// would also pass against a digest that had simply stopped counting the second job for an
    /// unrelated reason.
    /// </summary>
    [Fact]
    public async Task Both_Jobs_Are_Counted_When_Neither_Is_Held()
    {
        await SeedAsync(heldForRival: false);

        var pushes = await RunSweepAsync();

        Assert.Equal("2", Assert.Single(pushes)["count"]);
    }

    /// <summary>
    /// The one that goes red against the naive fix. Sweep 1 notifies about the open job and advances the
    /// watermark past BOTH jobs' status tracks; the hold then expires. The held job's own history has
    /// not changed and never will, so only the hold-expiry source can bring it back — and it must,
    /// exactly once.
    /// </summary>
    [Fact]
    public async Task A_Job_Re_Enters_The_Digest_When_Its_Hold_Expires_After_The_Watermark()
    {
        await SeedAsync(heldForRival: true);

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);

        var watermark = await ReadWatermarkAsync();
        Assert.NotNull(watermark);
        await ExpireHoldAsync("order-held", watermark.Value.UtcDateTime.AddTicks(1));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
        Assert.Empty(await RunSweepAsync());
    }

    /// <summary>
    /// The same recovery through the OTHER freshness branch. Once a commitment of this cleaner's is
    /// released the query takes a different code path, and the hold-expiry source is written out twice —
    /// so an edit that fixes one branch and forgets the other leaves the held job invisible for exactly
    /// the cleaners who have ever had a cancellation. The released window is deliberately nowhere near
    /// the held job's slot, so only the hold source can account for it.
    /// </summary>
    [Fact]
    public async Task The_Hold_Expiry_Also_Recovers_A_Job_Through_The_Released_Window_Branch()
    {
        await SeedAsync(heldForRival: true, withCommitmentAt: ClashSlot);

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);

        var watermark = await ReadWatermarkAsync();
        Assert.NotNull(watermark);
        await ExpireHoldAsync("order-held", watermark.Value.UtcDateTime.AddTicks(1));
        await CancelAsync("order-commitment", watermark.Value.AddTicks(2));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
    }

    /// <summary>
    /// The perk, from the beneficiary's side: the hold withholds the job from the board, never from the
    /// person it is being held for.
    /// </summary>
    [Fact]
    public async Task The_Beneficiary_Still_Counts_Their_Own_Held_Job()
    {
        await SeedAsync(heldForRival: true, holdBeneficiary: EmployeeId);

        var pushes = await RunSweepAsync();

        Assert.Equal("2", Assert.Single(pushes)["count"]);
    }

    /// <summary>
    /// ADR-0045 D6.1 — the regression guard most likely to be lost. The lapse sweep runs between the
    /// two digests and announces the closure to the customer; the held job must STILL come back to the
    /// board's notification channel afterwards.
    ///
    /// <para>The tidy-looking implementation of that sweep clears the hold pair after notifying, so its
    /// own predicate becomes self-idempotent with zero new columns. It would erase the disjunct above
    /// before the 30-minute digest ever saw it, and the order would leave the notification channel
    /// permanently — board-only, findable solely by someone who happens to scroll. That is the defect
    /// ADR-0036 spent a panel round closing, restored through a back door, and it is why the sweep
    /// carries a receipt column instead.</para>
    /// </summary>
    [Fact]
    public async Task The_Lapse_Sweep_Leaves_The_Job_Recoverable_By_The_Digest()
    {
        await SeedAsync(heldForRival: true);

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);

        var watermark = await ReadWatermarkAsync();
        Assert.NotNull(watermark);
        await ExpireHoldAsync("order-held", watermark.Value.UtcDateTime.AddTicks(1));

        await RunLapseSweepAsync();

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
    }

    /// <summary>
    /// The announcement really did happen, so the assertion above is not passing because the sweep
    /// silently matched nothing.
    /// </summary>
    private async Task RunLapseSweepAsync()
    {
        var tenantProvider = new FixedTenantProvider(null);
        await using var ctx = new CleansiaDbContext(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            tenantProvider);

        var handler = new NotifyLapsedPreferredOffers.Handler(
            new OrderRepository(ctx),
            new NotificationProducer(new UserNotificationRepository(ctx), new OutboxPendingDispatch(ctx)),
            tenantProvider,
            ctx,
            NullLogger<NotifyLapsedPreferredOffers.Handler>.Instance);

        var result = await handler.Handle(new NotifyLapsedPreferredOffers.Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.NotifiedCount);
    }

    private async Task<IReadOnlyList<Dictionary<string, string>>> RunSweepAsync()
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

        var digest = new NewJobsDigestService(
            new EmployeeRepository(ctx),
            new OrderRepository(ctx),
            new UserNotificationPreferencesRepository(ctx),
            producer.Object,
            ctx,
            NullLogger<NewJobsDigestService>.Instance);

        await digest.SendDigestsAsync(CancellationToken.None);

        return pushes;
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

    /// <summary>
    /// Time passing, expressed as the only observable it has: the deadline is now behind us. It moves
    /// through the private setter because the aggregate has no "expire" operation BY DESIGN — expiry is
    /// a clock comparison with no actor, no sweep and no state transition — and because the order's
    /// status history must stay untouched, which is the whole premise of the test.
    /// </summary>
    private async Task ExpireHoldAsync(string orderId, DateTime expiredAtUtc)
    {
        await using var ctx = NewContext(tenantId: null);
        var order = await ctx.Set<Order>().IgnoreQueryFilters().FirstAsync(o => o.Id == orderId);

        typeof(Order)
            .GetProperty(nameof(Order.PreferredHoldUntilUtc))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(order, [expiredAtUtc]);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task CancelAsync(string orderId, DateTimeOffset cancelledAt)
    {
        await using var ctx = NewContext(tenantId: null);
        var order = await ctx.Set<Order>()
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .FirstAsync(o => o.Id == orderId);

        AppendTrack(order, OrderStatus.Cancelled, cancelledAt);

        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task SeedAsync(
        bool heldForRival,
        string? holdBeneficiary = null,
        DateTime? withCommitmentAt = null)
    {
        await using (var schema = NewContext(tenantId: null))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext(tenantId: null);

        var user = User.CreateWithPassword(
            "hold.cleaner@cleansia.test", "Test-password-1!", "Hana", "Hold", UserProfile.Employee);
        user.Id = UserId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = EmployeeId;
        cleaner.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        cleaner.Approve(approvedByUserId: "admin-digest-hold");
        cleaner.AssignWorkCountry(CountryId);
        seed.Add(cleaner);

        var held = NewOfferableOrder("order-held", HeldSlot);
        if (heldForRival)
        {
            held.GrantPreferredHold(
                holdBeneficiary ?? RivalEmployeeId,
                DateTime.UtcNow.AddHours(3),
                DateTime.UtcNow,
                BookingPolicy.MaxPreferredOfferRounds);
        }

        seed.Add(held);
        seed.Add(NewOfferableOrder("order-open", OpenSlot));

        if (withCommitmentAt is { } clash)
        {
            var commitment = NewOfferableOrder("order-commitment", clash);
            commitment.AddAssignedEmployee(OrderEmployee.Create(commitment, cleaner));
            seed.Add(commitment);
        }

        await seed.CommitAsync(CancellationToken.None);
    }

    private static Order NewOfferableOrder(string orderId, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Hold Customer",
            customerEmail: "hold-customer@cleansia.test",
            customerPhone: "+420777444666",
            customerAddress: Address.Create("Hold St 5", "Praha", "14000", CountryId),
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

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
