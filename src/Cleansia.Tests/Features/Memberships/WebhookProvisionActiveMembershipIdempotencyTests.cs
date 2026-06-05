using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Stripe;
using AppConstants = Cleansia.Core.AppServices.Common.Constants;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// T-0114 (SEC-W2) / TC-IDEMP-0 (pairs with T-0127) / ADR-0002 D2 idempotent-consumer contract
/// ("every consumer asserts before acting"), knowledge/testing.md must-cover #6 (idempotency, S7) and
/// S7a/S7b (assert-before-act + the unique-index backstop caught at the right boundary).
///
/// THE HOLE: the web Checkout flow creates ONLY the Stripe Session; the local <see cref="UserMembership"/>
/// row is created EXCLUSIVELY by the <c>customer.subscription.created</c> webhook in
/// <see cref="StripeSubscriptionWebhookHandler"/>.<c>ProvisionFromCreatedEventAsync</c>. That path called
/// <see cref="UserMembership.Create"/> WITHOUT ever calling
/// <see cref="IUserMembershipRepository.GetActiveForUserAsync"/> — unlike the request path
/// (<c>CreateMembershipCheckoutSession</c>) which guards. So a user who already has an active membership and
/// reaches Stripe again (stale tab / Dashboard / two near-simultaneous checkouts) gets a SECOND active row
/// → double benefits, reconciliation drift.
///
/// THE FIX (S7a — assert + DB backstop):
///  1. ASSERT: in <c>ProvisionFromCreatedEventAsync</c>, AFTER the tenant override is set (so
///     <c>GetActiveForUserAsync</c> resolves in the right tenant scope) and BEFORE
///     <see cref="UserMembership.Create"/>, call <c>GetActiveForUserAsync(userId)</c>. If non-null: log a
///     reconcile/skip WARNING and RETURN the existing row WITHOUT Create/Add. The event is still stamped
///     processed by the outer <c>HandlePaymentNotification</c> handler (a duplicate provision is a no-op
///     success, not an error — that's the idempotent-consumer contract).
///  2. DB BACKSTOP: a FILTERED UNIQUE INDEX on (TenantId, UserId) WHERE Status = Active
///     (UserMembershipEntityConfiguration), so a second active row is rejected by Postgres (23505) even if
///     the app check is bypassed by a race. Filtered to Active so Cancelled/expired + a new Active is still
///     permitted.
///  3. S7b: the webhook handler is the consumer. It does NOT own its own commit — the outer
///     <c>HandlePaymentNotification.Handle</c> runs inside the <c>UnitOfWorkPipelineBehavior</c>, whose
///     <c>CommitAsync</c> fires AFTER the handler returns. So to MAP a 23505 (race loser) into a clean
///     reconcile no-op instead of a raw DbUpdateException/500 (which would make Stripe RETRY and amplify),
///     the provisioning path FLUSHES the insert itself (its own <c>CommitAsync</c>) inside a
///     <c>catch (DbUpdateException) when (IsUniqueViolation)</c> and resolves to the existing active row.
///
/// These are LOGIC-LEVEL handler unit tests (mocked repositories): the assert is modelled by the mocked
/// <c>GetActiveForUserAsync</c>; the race-loser 23505 is modelled by the mocked <c>CommitAsync</c> throwing
/// a <see cref="DbUpdateException"/> whose inner exception duck-types <c>SqlState == "23505"</c>. The DB
/// backstop itself (AC3 / AC5c) is proven by <see cref="UserMembershipActiveUniqueIndexTests"/> against a
/// real <see cref="CleansiaDbContext"/>. Written RED first (predates the handler change).
/// </summary>
public class WebhookProvisionActiveMembershipIdempotencyTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string PlanId = "plan-1";
    private const string TenantId = "tenant-A";
    private const string ExistingSubId = "sub_existing";
    private const string NewSubId = "sub_new";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    public WebhookProvisionActiveMembershipIdempotencyTests()
    {
        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.TenantId = TenantId;
        _userRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_test_1",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);
        plan.Id = PlanId;
        _planRepository
            .Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // No row matches the NEW subscription id (this is a fresh subscription.created).
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(NewSubId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
    }

    private StripeSubscriptionWebhookHandler CreateHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _tenantProvider.Object,
            NullLogger<StripeSubscriptionWebhookHandler>.Instance);

    /// <summary>
    /// Build a <c>customer.subscription.created</c> event for the given subscription id, carrying the
    /// UserId + MembershipPlanCode metadata the provisioning path reads, and a single subscription item
    /// with period bounds (matching <c>ExtractSubscriptionShape</c>'s first-item read).
    /// </summary>
    private static Event SubscriptionCreatedEvent(string subscriptionId)
    {
        var subscription = new Subscription
        {
            Id = subscriptionId,
            Status = "active",
            Metadata = new Dictionary<string, string>
            {
                ["UserId"] = UserId,
                ["MembershipPlanCode"] = PlanCode,
            },
            Items = new StripeList<SubscriptionItem>
            {
                Data =
                [
                    new SubscriptionItem
                    {
                        CurrentPeriodStart = DateTime.UtcNow,
                        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                    },
                ],
            },
        };

        return new Event
        {
            Id = $"evt_{subscriptionId}",
            Type = AppConstants.StripeEventType.SubscriptionCreated,
            Data = new EventData { Object = subscription },
        };
    }

    private UserMembership ExistingActiveMembership() =>
        UserMembership.Create(
            userId: UserId,
            membershipPlanId: PlanId,
            stripeSubscriptionId: ExistingSubId,
            currentPeriodStart: DateTime.UtcNow.AddDays(-5),
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));

    // ── AC1 — already-active user + a new subscription.created ⇒ assert finds the active row, NO 2nd Add ──

    [Fact]
    public async Task AC1_AlreadyActiveUser_NewSubscriptionCreated_DoesNotAddSecondRow_Reconciles()
    {
        // The active-check assert finds the user's existing active membership BEFORE Create.
        var existing = ExistingActiveMembership();
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateHandler().HandleAsync(SubscriptionCreatedEvent(NewSubId), CancellationToken.None);

        // No second active row is inserted — the provisioning path short-circuits on the assert.
        _membershipRepository.Verify(r => r.Add(It.IsAny<UserMembership>()), Times.Never);
    }

    // ── AC2 — clean user (no active membership) ⇒ EXACTLY ONE row created (happy path unchanged) ──

    [Fact]
    public async Task AC2_CleanUser_SubscriptionCreated_CreatesExactlyOneRow()
    {
        // No active membership for this user — the assert returns null and Create proceeds.
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var added = new List<UserMembership>();
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(added.Add);

        await CreateHandler().HandleAsync(SubscriptionCreatedEvent(NewSubId), CancellationToken.None);

        // Exactly one row, for the new subscription, Active.
        Assert.Single(added);
        Assert.Equal(NewSubId, added[0].StripeSubscriptionId);
        Assert.Equal(MembershipStatus.Active, added[0].Status);
    }

    // ── AC1 — the assert resolves in the RIGHT tenant scope: tenant override is set before the read ──

    [Fact]
    public async Task AC1_ActiveCheck_RunsAfterTenantOverrideIsSet()
    {
        var overrideSetBeforeActiveCheck = false;
        _tenantProvider
            .Setup(p => p.SetTenantOverride(TenantId))
            .Callback(() => { /* tenant scope now resolves to TenantId */ });

        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .Callback(() => overrideSetBeforeActiveCheck =
                Mock.Get(_tenantProvider.Object).Invocations
                    .Any(i => i.Method.Name == nameof(ITenantProvider.SetTenantOverride)))
            .ReturnsAsync(ExistingActiveMembership());

        await CreateHandler().HandleAsync(SubscriptionCreatedEvent(NewSubId), CancellationToken.None);

        // The override (from owningUser.TenantId) must be applied BEFORE GetActiveForUserAsync so the
        // active-check resolves in the user's tenant scope (S8) rather than the ambient/null scope.
        Assert.True(overrideSetBeforeActiveCheck);
    }

    // ── AC1 / S7b — the PRE-WINNER-COMMIT race: assert returns null, Add proceeds, the in-handler flush
    //    hits the filtered unique index (23505); the handler MUST collapse to a reconcile no-op, NOT throw
    //    (a throw → 500 → Stripe retry storm). The outer pipeline's final commit is then a safe no-op. ──

    [Fact]
    public async Task AC1_RaceLoser_UniqueViolationOnFlush_ResolvesToReconcileNoOp_DoesNotThrow()
    {
        // The loser never sees an active membership at the assert (winner not yet committed) — the TOCTOU
        // window the read alone cannot close.
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var added = new List<UserMembership>();
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(added.Add);

        // The in-handler flush hits the filtered (TenantId, UserId) WHERE Status=Active unique index and
        // Postgres raises 23505 (wrapped in DbUpdateException).
        _membershipRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        // RED before the fix: the handler does no flush/catch, Adds unconditionally and never calls its own
        // CommitAsync, so the modelled 23505 is never triggered here — instead the duplicate row would
        // surface at the PIPELINE commit as an unhandled 500 (→ Stripe retry storm). GREEN after the fix:
        // the handler OWNS the flush (its own CommitAsync at the write boundary), catches the 23505, and
        // collapses to a reconcile no-op — no throw escapes the webhook handler.
        var ex = await Record.ExceptionAsync(() =>
            CreateHandler().HandleAsync(SubscriptionCreatedEvent(NewSubId), CancellationToken.None));

        Assert.Null(ex);
        // The fix must flush IN the handler (S7b — the violation only surfaces where you flush, not at the
        // outer pipeline commit). This is the assertion that is RED before the fix (CommitAsync never called)
        // and GREEN after (the handler flushes and catches the unique-violation itself).
        _membershipRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Stand-in for Npgsql's <c>PostgresException</c>. The handler detects a unique-violation
    /// provider-agnostically by duck-typing the inner exception's public <c>SqlState</c> string against
    /// Postgres code "23505" (the AppServices layer carries no hard Npgsql reference). This fake exposes the
    /// same <c>SqlState == "23505"</c> shape so the catch→reconcile path is exercised without a real
    /// PostgresException. Mirrors the fakes in T-0111/T-0112's idempotency suites.
    /// </summary>
    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}

/// <summary>
/// T-0114 (SEC-W2) AC3 / AC5c — DB-level proof of the FILTERED UNIQUE INDEX backstop on
/// <c>UserMemberships (TenantId, UserId) WHERE Status = Active</c>, exercised against a REAL
/// <see cref="CleansiaDbContext"/> (so <c>OnModelCreating</c> + the entity config's <c>HasIndex(...)
/// .IsUnique().HasFilter("\"Status\" = 1")</c> actually run) over SQLite in-memory — the same harness
/// T-0113 used. SQLite, like Postgres, supports partial (filtered) unique indexes, so the filtered
/// semantics are testable here without the Postgres Testcontainers harness.
///
/// If the filtered-index semantics could NOT be exercised in this unit harness, the honest move (per the
/// T-0112 precedent) would be to defer this case to the T-0127 integration suite rather than fake it. They
/// CAN be exercised, so they are proven here.
/// </summary>
public sealed class UserMembershipActiveUniqueIndexTests : IDisposable
{
    private const string TenantId = "tenant-A";
    private const string UserId = "user-1";
    private const string PlanId = "plan-1";

    private readonly SqliteConnection _connection;

    public UserMembershipActiveUniqueIndexTests()
    {
        // FK enforcement OFF: this test isolates the FILTERED UNIQUE INDEX behaviour, not the User /
        // MembershipPlan Restrict FKs. Seeding a fully-valid User graph in SQLite drags in unrelated
        // required relations; turning FK enforcement off lets us insert UserMembership rows directly so
        // the only constraint under test is the (TenantId, UserId) WHERE Status=Active unique index.
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False");
        _connection.Open();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext(string? tenantId)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CleansiaDbContext(
            options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId));
    }

    /// <summary>
    /// Create the schema once (so <c>OnModelCreating</c> emits the filtered unique index). FK parents are
    /// not seeded — FK enforcement is disabled on the connection (see constructor) so the test isolates the
    /// unique index.
    /// </summary>
    private async Task EnsureSchemaAsync()
    {
        await using var ctx = NewContext(TenantId);
        await ctx.Database.EnsureCreatedAsync();
    }

    private static UserMembership ActiveMembership(string subscriptionId) =>
        UserMembership.Create(
            userId: UserId,
            membershipPlanId: PlanId,
            stripeSubscriptionId: subscriptionId,
            currentPeriodStart: DateTime.UtcNow,
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));

    // ── AC3 / AC5c — a SECOND Active row for the same (TenantId, UserId) is DB-REJECTED (unique violation) ──

    [Fact]
    public async Task SecondActiveRow_SameTenantAndUser_IsRejectedByFilteredUniqueIndex()
    {
        await EnsureSchemaAsync();

        await using (var ctx = NewContext(TenantId))
        {
            ctx.Add(ActiveMembership("sub_first"));
            await ctx.CommitAsync(CancellationToken.None);
        }

        // A second ACTIVE row for the same (TenantId, UserId) must hit the filtered unique index.
        await using var ctx2 = NewContext(TenantId);
        ctx2.Add(ActiveMembership("sub_second"));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => ctx2.CommitAsync(CancellationToken.None));
        Assert.Contains("UNIQUE", ex.InnerException?.Message ?? ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC3 — the index is FILTERED: a Cancelled membership + a NEW Active subscription is PERMITTED ──

    [Fact]
    public async Task CancelledRow_PlusNewActiveRow_SameTenantAndUser_IsPermitted()
    {
        await EnsureSchemaAsync();

        // First an Active row, then drop it OUT of the partial-index predicate by cancelling it.
        await using (var ctx = NewContext(TenantId))
        {
            ctx.Add(ActiveMembership("sub_old"));
            await ctx.CommitAsync(CancellationToken.None);
        }
        await using (var ctx = NewContext(TenantId))
        {
            var old = await ctx.Set<UserMembership>().FirstAsync(m => m.StripeSubscriptionId == "sub_old");
            // "canceled" → MembershipStatus.Cancelled, which falls outside WHERE Status = Active.
            old.UpdateFromStripeWebhook("canceled", old.CurrentPeriodStart, old.CurrentPeriodEnd);
            await ctx.CommitAsync(CancellationToken.None);
        }

        // The re-subscribe-after-cancel case: a new Active row for the same (TenantId, UserId) is allowed
        // because the cancelled row is no longer in the filtered index. A naive FULL unique index would
        // wrongly block this.
        await using var ctx2 = NewContext(TenantId);
        ctx2.Add(ActiveMembership("sub_new"));

        var ex = await Record.ExceptionAsync(() => ctx2.CommitAsync(CancellationToken.None));
        Assert.Null(ex);

        await using var verify = NewContext(TenantId);
        var rows = await verify.Set<UserMembership>().CountAsync();
        Assert.Equal(2, rows); // the cancelled one + the new active one
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;

        public string? GetCurrentTenantId() => _tenantId;

        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;

        public void ClearTenantOverride() => _tenantId = null;
    }
}
