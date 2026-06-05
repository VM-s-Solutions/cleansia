using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// ADR-0002 idempotent-consumer contract,
/// knowledge/testing.md must-cover #6 (idempotency, S7) and S7a/S7b (the atomic-backstop idiom).
///
/// THE HOLE: <see cref="LoyaltyService.GrantPointsManuallyAsync"/> /
/// <see cref="LoyaltyService.RevokePointsManuallyAsync"/> only ran their idempotency guard when an
/// <c>orderId</c> was supplied. The admin commands pass <c>orderId: null</c>, so the guard was SKIPPED
/// and a double-submit / proxy-retry / network-retry DOUBLE-GRANTED real points (which drive tier
/// discounts via <c>ResolveTierDiscountForOrderAsync</c> — a financial doubling).
///
/// THE FIX (S7a shape): the command carries a REQUIRED client-supplied <c>RequestId</c>; the service
/// threads it into the ledger row as the <see cref="LoyaltyTransaction.IdempotencyKey"/>; a retry
/// collapses via a fast-path lookup-by-key AND the filtered UNIQUE INDEX backstop (Postgres 23505 is
/// caught and resolved to the SAME success — the loser collapses, it does not error to the admin).
///
/// These are LOGIC-LEVEL unit tests (mocked repositories). The fast-path lookup is modelled by the
/// mocked <see cref="ILoyaltyTransactionRepository.GetByIdempotencyKeyAsync"/>; the concurrent-race
/// backstop is modelled by the mocked <c>CommitAsync</c> throwing a 23505 <see cref="DbUpdateException"/>.
/// The TRUE-PARALLEL DB proof (real filtered unique index under concurrent writers) is deferred to the
/// integration suite — the in-memory unit harness cannot enforce a unique constraint, so
/// faking genuine parallelism here would be theater. Written RED first (predates the service change).
/// </summary>
public class AdminLoyaltyGrantIdempotencyTests
{
    private const string UserId = "user-1";
    private const string ActorId = "admin-1";
    private const string RequestId = "req-idem-abc";
    private const int Points = 250;

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<IQueueClient> _queueClient = new();

    private LoyaltyService CreateService() =>
        new(
            _orderRepository.Object,
            _accountRepository.Object,
            _tierConfigRepository.Object,
            _transactionRepository.Object,
            _queueClient.Object,
            NullLogger<LoyaltyService>.Instance);

    // The account the service mutates. EnsureForUserAsync / GetByUserIdAsync return the SAME instance
    // across calls, so LifetimePoints accumulation across two submits is observable.
    private LoyaltyAccount ArrangeAccount()
    {
        var account = LoyaltyAccount.Create(UserId);
        account.Id = "acct-1";
        _accountRepository
            .Setup(r => r.EnsureForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return account;
    }

    // No prior ledger row for this idempotency key (the first-time path).
    private void ArrangeNoExistingKey()
    {
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyTransaction?)null);
    }

    // ── (grant) — same RequestId submitted twice ⇒ exactly ONE ledger row, points granted ONCE ──
    [Fact]
    public async Task AC1_Grant_SameRequestId_TwoSubmits_GrantsPointsOnce()
    {
        var account = ArrangeAccount();

        // First submit: no row exists for the key. Second submit: the fast-path lookup now finds the
        // winner's row (committed by the first submit's flush), so the service must short-circuit.
        LoyaltyTransaction? existing = null;
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existing);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => existing ??= account.Transactions.LastOrDefault())
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.GrantPointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, RequestId, CancellationToken.None);
        await service.GrantPointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, RequestId, CancellationToken.None);

        // Exactly ONE ledger row appended, LifetimePoints increased by Points ONCE (not 2 × Points).
        Assert.Single(account.Transactions);
        Assert.Equal(Points, account.LifetimePoints);
        Assert.Equal(RequestId, account.Transactions.Single().IdempotencyKey);
    }

    // ── (revoke) — mirror: same RequestId twice ⇒ one negative row, LifetimePoints reduced once ──
    [Fact]
    public async Task AC1_Revoke_SameRequestId_TwoSubmits_RevokesPointsOnce()
    {
        var account = ArrangeAccount();
        // Seed a positive balance to revoke against (order-driven earn, null key).
        account.GrantPoints(1000, LoyaltyEarnSource.OrderCompleted, "order-x", "system");
        var earnedRows = account.Transactions.Count; // 1

        LoyaltyTransaction? existing = null;
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existing);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => existing ??= account.Transactions.LastOrDefault(t => t.IdempotencyKey == RequestId))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.RevokePointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, RequestId, CancellationToken.None);
        await service.RevokePointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, RequestId, CancellationToken.None);

        // Exactly ONE revoke row appended on top of the seeded earn; balance reduced by Points ONCE.
        Assert.Equal(earnedRows + 1, account.Transactions.Count);
        Assert.Equal(1000 - Points, account.LifetimePoints);
        var revokeRow = account.Transactions.Single(t => t.IdempotencyKey == RequestId);
        Assert.Equal(-Points, revokeRow.Points);
    }

    // ── (grant) — concurrent identical grants: the loser hits the unique index (23505) and
    //    COLLAPSES (no throw, no second side effect). Models the pre-winner-commit race window. ──
    [Fact]
    public async Task AC2_Grant_Concurrent_SameRequestId_UniqueViolation_Collapses_NoSecondSideEffect()
    {
        var account = ArrangeAccount();

        // The loser NEVER sees an existing row at the fast-path read (winner not yet committed) — this
        // is the TOCTOU window the fast-path read alone cannot close.
        ArrangeNoExistingKey();

        // The loser's in-service flush hits the filtered unique index ⇒ Postgres 23505 (wrapped).
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var service = CreateService();

        // GREEN: the service catches the 23505 and returns cleanly (loser collapses). No throw escapes.
        var ex = await Record.ExceptionAsync(() =>
            service.GrantPointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, RequestId, CancellationToken.None));

        Assert.Null(ex);
        // The service rolled its own change-tracker back on collapse, so the pipeline's later commit is
        // a no-op for the loser — the winner's single grant is the only surviving side effect.
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── (revoke) — mirror: concurrent identical revoke loser collapses on the unique index ──
    [Fact]
    public async Task AC2_Revoke_Concurrent_SameRequestId_UniqueViolation_Collapses_NoThrow()
    {
        var account = ArrangeAccount();
        account.GrantPoints(1000, LoyaltyEarnSource.OrderCompleted, "order-x", "system");

        ArrangeNoExistingKey();
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var service = CreateService();

        var ex = await Record.ExceptionAsync(() =>
            service.RevokePointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, RequestId, CancellationToken.None));

        Assert.Null(ex);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── fast-path: an EXISTING row for the key short-circuits before any grant/flush ──
    [Fact]
    public async Task AC1_Grant_ExistingKeyRow_ShortCircuits_WithoutGrantingOrFlushing()
    {
        var account = ArrangeAccount();
        var existing = LoyaltyTransaction.Create(
            account.Id, LoyaltyTransactionType.Earn, Points, LoyaltyEarnSource.ManualGrant, null,
            idempotencyKey: RequestId);
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateService().GrantPointsManuallyAsync(
            UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, RequestId, CancellationToken.None);

        // No new ledger row, no balance change, no flush — the replay is a clean no-op.
        Assert.Empty(account.Transactions);
        Assert.Equal(0, account.LifetimePoints);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Stand-in for Npgsql's <c>PostgresException</c>. The service detects a unique-violation
    /// provider-agnostically by duck-typing the inner exception's public <c>SqlState</c> string against
    /// Postgres code "23505" (the AppServices layer carries no hard Npgsql reference). This fake exposes
    /// the same <c>SqlState == "23505"</c> shape so the catch→collapse path is exercised without a real
    /// PostgresException.
    /// </summary>
    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}
