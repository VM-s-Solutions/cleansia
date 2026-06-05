using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// T-0111 (LG-SEC-02) / TC-IDEMP-0 (pairs with T-0127) / ADR-0002 idempotent-consumer contract,
/// knowledge/testing.md must-cover #6 (idempotency, S7).
///
/// THE HOLE: <see cref="CreateMembershipSubscription.Handler"/> generated a FRESH
/// <c>Guid.NewGuid()</c> per call as the Stripe idempotency attempt id, so two concurrent confirmed
/// subscribes (a double-tapped mobile PaymentSheet) — both passing the <see cref="GetActiveForUserAsync"/>
/// "no active membership" guard before commit — called Stripe with TWO DIFFERENT keys and created TWO
/// subscriptions + TWO UserMembership rows. The customer is billed twice.
///
/// THE FIX (owner decision, BINDING): the Command carries a client-supplied
/// <see cref="CreateMembershipSubscription.Command.IdempotencyToken"/>; the handler derives the Stripe
/// <c>idempotencyAttemptId</c> from it (deterministic fallback when null), so the SAME logical attempt
/// REPLAYS the SAME Stripe subscription (the real Stripe key is
/// <c>sub-{customer}-{price}-{attemptId}</c> — same attemptId ⇒ Stripe returns the same subscription).
/// A genuine re-subscribe after cancellation carries a NEW token ⇒ a NEW subscription. The loser of a
/// concurrent confirm re-checks active membership after the Stripe call and gets a deterministic
/// <see cref="BusinessErrorMessage.MembershipAlreadyActive"/> instead of a duplicate row / raw 500.
///
/// These tests model the mocked <see cref="IStripeClient"/> so its returned SubscriptionId is DERIVED
/// from the <c>idempotencyAttemptId</c> argument — same attemptId ⇒ same subscription (Stripe replay);
/// different attemptId ⇒ different subscription. That makes "the subscription side-effect fires once"
/// observable at the unit level. Written RED first (predates the handler change).
/// </summary>
public class CreateMembershipSubscriptionIdempotencyTests
{
    private const string UserId = "user-1";
    private const string PlanCode = "PLUS_MONTHLY";
    private const string StripeCustomerId = "cus_test_1";
    private const string StripePriceId = "price_test_1";
    private const string ClientToken = "idem-token-abc";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IStripeClient> _stripe = new();

    public CreateMembershipSubscriptionIdempotencyTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);

        var user = User.CreateWithPassword("sub@example.com", "12345678Test!", "Sub", "Scriber");
        user.Id = UserId;
        user.AssignStripeCustomerId(StripeCustomerId);
        _userRepository
            .Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var plan = MembershipPlan.Create(
            code: PlanCode,
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: StripePriceId,
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);
        _planRepository
            .Setup(r => r.GetByCodeAsync(PlanCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Model Stripe's real idempotency: the returned SubscriptionId is derived from the
        // idempotencyAttemptId, so two calls with the SAME attemptId yield the SAME subscription
        // (Stripe replay), and a different attemptId yields a different subscription.
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int _, string attemptId, CancellationToken _) =>
                new SubscriptionResult(
                    SubscriptionId: $"sub_{attemptId}",
                    CurrentPeriodStart: DateTime.UtcNow,
                    CurrentPeriodEnd: DateTime.UtcNow.AddMonths(1)));
    }

    private CreateMembershipSubscription.Handler CreateHandler() =>
        new(
            _userRepository.Object,
            _membershipRepository.Object,
            _planRepository.Object,
            _session.Object,
            _stripe.Object,
            NullLogger<CreateMembershipSubscription.Handler>.Instance);

    private static CreateMembershipSubscription.Command ConfirmedCommand(string? token) =>
        new(PlanCode, PaymentMethodConfirmed: true) { IdempotencyToken = token };

    // ── AC2 — the Stripe idempotency key is DERIVED from the client token, not Guid.NewGuid() ──

    [Fact]
    public async Task SameToken_TwoConfirms_PassSameDerivedAttemptIdToStripe()
    {
        // No active membership at the first guard for either request.
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var capturedAttemptIds = new List<string>();
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                StripeCustomerId, StripePriceId, It.IsAny<int>(),
                Capture.In(capturedAttemptIds), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int _, string attemptId, CancellationToken _) =>
                new SubscriptionResult($"sub_{attemptId}", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        await CreateHandler().Handle(ConfirmedCommand(ClientToken), CancellationToken.None);
        await CreateHandler().Handle(ConfirmedCommand(ClientToken), CancellationToken.None);

        Assert.Equal(2, capturedAttemptIds.Count);
        // Both attempts derive the SAME Stripe attempt id from the SAME client token — so Stripe
        // collapses them onto ONE subscription. (The old Guid.NewGuid() produced two distinct keys.)
        Assert.Equal(capturedAttemptIds[0], capturedAttemptIds[1]);
        // And the derived id is tied to the client token, not a random Guid.
        Assert.Contains(ClientToken, capturedAttemptIds[0]);
    }

    [Fact]
    public async Task DifferentTokens_ProduceDifferentDerivedAttemptIds_AndNewSubscriptions()
    {
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var created = new List<UserMembership>();
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(created.Add);

        // A genuine re-subscribe after a real cancellation is a NEW logical attempt (new token).
        var first = await CreateHandler().Handle(ConfirmedCommand("token-attempt-1"), CancellationToken.None);
        var second = await CreateHandler().Handle(ConfirmedCommand("token-attempt-2"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, created.Count);
        // Distinct tokens ⇒ distinct Stripe subscriptions ⇒ distinct local rows (re-subscribe works, AC2).
        Assert.NotEqual(created[0].StripeSubscriptionId, created[1].StripeSubscriptionId);
    }

    // ── AC1 — same token, concurrent confirms: exactly ONE subscription, ONE persisted row ──

    [Fact]
    public async Task SameToken_TwoConfirms_PersistExactlyOneSubscription()
    {
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var created = new List<UserMembership>();
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(created.Add);

        await CreateHandler().Handle(ConfirmedCommand(ClientToken), CancellationToken.None);
        await CreateHandler().Handle(ConfirmedCommand(ClientToken), CancellationToken.None);

        // Stripe replayed (same derived key ⇒ same subscription id) so even though two rows were Added,
        // they reference the SAME Stripe subscription — exactly one billable subscription side-effect.
        Assert.All(created, m => Assert.Equal(created[0].StripeSubscriptionId, m.StripeSubscriptionId));
        // And there is only one DISTINCT Stripe subscription across all confirms.
        Assert.Single(created.Select(m => m.StripeSubscriptionId).Distinct());
    }

    // ── AC1 — the loser (re-check sees the now-active membership) gets a deterministic result ──

    [Fact]
    public async Task SameToken_Loser_RechecksAfterStripe_AndReturnsMembershipAlreadyActive_WithoutSecondAdd()
    {
        UserMembership? active = null;
        // First request: no active membership at either the pre-Stripe guard or the post-Stripe re-check.
        // Second (loser) request: still no active membership at the FIRST guard (TOCTOU window), but the
        // post-Stripe re-check now finds the winner's row — so it must NOT Add a duplicate.
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => active);

        UserMembership? winnerRow = null;
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(m => winnerRow ??= m);

        var winner = await CreateHandler().Handle(ConfirmedCommand(ClientToken), CancellationToken.None);
        Assert.True(winner.IsSuccess);

        // The winner's row is now the user's active membership (commit happened in the pipeline).
        active = winnerRow;

        var loser = await CreateHandler().Handle(ConfirmedCommand(ClientToken), CancellationToken.None);

        Assert.True(loser.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipAlreadyActive, loser.Error!.Message);
        // Exactly one row was ever Added — the loser did NOT create a duplicate.
        _membershipRepository.Verify(r => r.Add(It.IsAny<UserMembership>()), Times.Once);
    }

    // ── AC1 — ROUND-2: the PRE-WINNER-COMMIT race. The window the round-1 re-check only NARROWED. ──
    //
    // Round-1's test (SameToken_Loser_RechecksAfterStripe...) ran SERIALIZED: it let the winner run to
    // completion, MANUALLY set active=winnerRow, then ran the loser — i.e. it modelled confirm-AFTER-commit,
    // where the post-Stripe re-check (GetActiveForUserAsync) already sees the winner. That is NOT the genuine
    // concurrent window the ticket exists to close. In the real race the LOSER re-checks BEFORE the winner's
    // pipeline CommitAsync has made the winner's row visible: GetActiveForUserAsync returns NULL, the loser
    // proceeds to Add + flush, and the unique index on StripeSubscriptionId (UserMembershipEntityConfiguration
    // :56-57) rejects the insert with a Postgres 23505 unique-violation wrapped in a DbUpdateException.
    //
    // BEFORE the fix the handler had no flush and no catch, so that DbUpdateException escaped the handler and
    // surfaced as a raw 500 at the pipeline's UnitOfWorkPipelineBehavior.CommitAsync. This test models exactly
    // that path — re-check NULL + the in-handler flush (CommitAsync) raising the 23505 DbUpdateException — and
    // asserts the handler CLOSES the window: it catches the unique-violation, resolves the winner via
    // GetByStripeSubscriptionIdAsync, and returns a deterministic MembershipAlreadyActive instead of throwing.
    [Fact]
    public async Task SameToken_Loser_PreWinnerCommit_UniqueViolationOnFlush_ResolvesToMembershipAlreadyActive()
    {
        // The loser NEVER sees an active membership at either guard or re-check — the winner's row is not yet
        // visible (pre-winner-commit). This is the window the round-1 serialized test did not exercise.
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var added = new List<UserMembership>();
        _membershipRepository.Setup(r => r.Add(It.IsAny<UserMembership>()))
            .Callback<UserMembership>(added.Add);

        // The in-handler flush hits the unique index and Postgres raises 23505 (wrapped in DbUpdateException).
        _membershipRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        // After the violation the handler resolves the winner by Stripe subscription id. The derived
        // attempt id (and thus sub id) is identical for the same token, so this is the winner's row.
        var winnerRow = UserMembership.Create(
            userId: UserId,
            membershipPlanId: "plan-1",
            stripeSubscriptionId: $"sub_tok-{ClientToken}",
            currentPeriodStart: DateTime.UtcNow,
            currentPeriodEnd: DateTime.UtcNow.AddMonths(1));
        _membershipRepository
            .Setup(r => r.GetByStripeSubscriptionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(winnerRow);

        // RED before the fix: the handler does not flush/catch, so the DbUpdateException escapes (a throw),
        // which surfaces as a 500. GREEN after the fix: a deterministic MembershipAlreadyActive, no throw.
        var loser = await CreateHandler().Handle(ConfirmedCommand(ClientToken), CancellationToken.None);

        Assert.True(loser.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipAlreadyActive, loser.Error!.Message);
        // The loser DID attempt the insert (it passed the re-check), then flushed and hit the unique index —
        // the window was closed at the write boundary, not merely re-checked away.
        Assert.Single(added);
        _membershipRepository.Verify(
            r => r.GetByStripeSubscriptionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── AC1 — null/empty token (web / not-yet-updated caller): deterministic fallback key, still collapses ──

    [Fact]
    public async Task NullToken_DerivesDeterministicFallbackKey_FromStableInputs()
    {
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);

        var capturedAttemptIds = new List<string>();
        _stripe
            .Setup(c => c.CreateSubscriptionAsync(
                StripeCustomerId, StripePriceId, It.IsAny<int>(),
                Capture.In(capturedAttemptIds), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, int _, string attemptId, CancellationToken _) =>
                new SubscriptionResult($"sub_{attemptId}", DateTime.UtcNow, DateTime.UtcNow.AddMonths(1)));

        await CreateHandler().Handle(ConfirmedCommand(token: null), CancellationToken.None);
        await CreateHandler().Handle(ConfirmedCommand(token: string.Empty), CancellationToken.None);

        Assert.Equal(2, capturedAttemptIds.Count);
        // Defense in depth: with no client token, the fallback is DETERMINISTIC across calls
        // (derived from stable inputs userId + planCode), NOT a per-call Guid — so even a
        // not-yet-updated caller's double-tap collapses on the same Stripe key.
        Assert.Equal(capturedAttemptIds[0], capturedAttemptIds[1]);
    }

    // ── AC5 — Phase-1 (PaymentMethodConfirmed == false) unchanged: SetupIntent + ephemeral key, no sub ──

    [Fact]
    public async Task UnconfirmedPhase1_ReturnsSetupIntentAndEphemeralKey_AndCreatesNoSubscription()
    {
        _membershipRepository
            .Setup(r => r.GetActiveForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMembership?)null);
        _stripe
            .Setup(c => c.CreateSetupIntentAsync(StripeCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SetupIntentResult("seti_1", "seti_secret_1"));
        _stripe
            .Setup(c => c.CreateEphemeralKeyAsync(StripeCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ek_secret_1");

        var result = await CreateHandler().Handle(
            new CreateMembershipSubscription.Command(PlanCode, PaymentMethodConfirmed: false)
            {
                IdempotencyToken = ClientToken,
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("seti_secret_1", result.Value.SetupIntentClientSecret);
        Assert.Equal("ek_secret_1", result.Value.EphemeralKey);
        Assert.Equal(string.Empty, result.Value.MembershipId);
        _stripe.Verify(c => c.CreateSubscriptionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _membershipRepository.Verify(r => r.Add(It.IsAny<UserMembership>()), Times.Never);
    }

    /// <summary>
    /// Stand-in for Npgsql's <c>PostgresException</c>. The handler detects a unique-violation provider-
    /// agnostically by duck-typing the inner exception's public <c>SqlState</c> string property against
    /// Postgres code "23505" (the AppServices layer deliberately has no hard Npgsql reference). This fake
    /// exposes the same <c>SqlState == "23505"</c> shape so the catch→resolve path is exercised without
    /// constructing a real PostgresException.
    /// </summary>
    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}
