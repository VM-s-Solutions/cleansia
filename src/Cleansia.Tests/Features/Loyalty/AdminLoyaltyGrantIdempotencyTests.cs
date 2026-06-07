using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// The admin manual grant/revoke path keys idempotency on a client-supplied RequestId persisted as the
/// ledger row's IdempotencyKey. A double-submit collapses via a fast-path lookup-by-key and the filtered
/// unique-index backstop (a Postgres 23505 is caught and resolved to the same success; the loser rolls
/// back its own tracked changes rather than erroring to the admin).
///
/// These are logic-level unit tests with mocked repositories: the fast-path lookup is the mocked
/// GetByIdempotencyKeyAsync, and the concurrent-race backstop is the mocked CommitAsync throwing a
/// wrapped 23505 DbUpdateException. A true-parallel proof against a real filtered unique index belongs
/// to the integration suite — the in-memory unit harness cannot enforce a unique constraint.
/// </summary>
public class AdminLoyaltyGrantIdempotencyTests
{
    private const string UserId = "user-1";
    private const string ActorId = "admin-1";
    private const string RequestId = "req-idem-abc";
    private const string Reason = "goodwill credit #4821";
    private const int Points = 250;

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();

    private LoyaltyService CreateService() =>
        new(
            _orderRepository.Object,
            _accountRepository.Object,
            _tierConfigRepository.Object,
            _transactionRepository.Object,
            _pendingDispatch.Object,
            NullLogger<LoyaltyService>.Instance);

    // EnsureForUserAsync / GetByUserIdAsync return the same instance across calls, so points
    // accumulation across two submits is observable on the returned account.
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

    private void ArrangeNoExistingKey()
    {
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyTransaction?)null);
    }

    private void ArrangeTierConfigs()
    {
        var configs = new[]
        {
            LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, 500, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.GoldPolisher, 2000, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.PlatinumSparkler, 5000, 0m, null, "[]"),
        };
        _tierConfigRepository
            .Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);
    }

    private static LoyaltyTierThresholds DefaultThresholds() =>
        new(Silver: 500, Gold: 2000, Platinum: 5000);

    [Fact]
    public async Task Grant_SameRequestId_TwoSubmits_GrantsPointsOnce()
    {
        var account = ArrangeAccount();
        ArrangeTierConfigs();

        // First submit finds no row for the key; the second submit's fast-path lookup must see the
        // winner's committed row and short-circuit.
        LoyaltyTransaction? existing = null;
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existing);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => existing ??= account.Transactions.LastOrDefault())
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.GrantPointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);
        await service.GrantPointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);

        Assert.Single(account.Transactions);
        Assert.Equal(Points, account.LifetimePoints);
        Assert.Equal(RequestId, account.Transactions.Single().IdempotencyKey);
    }

    [Fact]
    public async Task Revoke_SameRequestId_TwoSubmits_RevokesPointsOnce()
    {
        var account = ArrangeAccount();
        ArrangeTierConfigs();
        account.GrantPoints(1000, LoyaltyEarnSource.OrderCompleted, "order-x", "system", DefaultThresholds());
        var earnedRows = account.Transactions.Count;

        LoyaltyTransaction? existing = null;
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existing);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => existing ??= account.Transactions.LastOrDefault(t => t.IdempotencyKey == RequestId))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.RevokePointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);
        await service.RevokePointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);

        Assert.Equal(earnedRows + 1, account.Transactions.Count);
        Assert.Equal(1000 - Points, account.LifetimePoints);
        var revokeRow = account.Transactions.Single(t => t.IdempotencyKey == RequestId);
        Assert.Equal(-Points, revokeRow.Points);
    }

    [Fact]
    public async Task Grant_Concurrent_SameRequestId_UniqueViolation_Collapses_NoSecondSideEffect()
    {
        ArrangeAccount();
        ArrangeTierConfigs();

        // The loser never sees an existing row at the fast-path read (winner not yet committed) — the
        // TOCTOU window the read alone cannot close.
        ArrangeNoExistingKey();

        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var service = CreateService();

        var ex = await Record.ExceptionAsync(() =>
            service.GrantPointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None));

        Assert.Null(ex);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Revoke_Concurrent_SameRequestId_UniqueViolation_Collapses_NoThrow()
    {
        var account = ArrangeAccount();
        ArrangeTierConfigs();
        account.GrantPoints(1000, LoyaltyEarnSource.OrderCompleted, "order-x", "system", DefaultThresholds());

        ArrangeNoExistingKey();
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var service = CreateService();

        var ex = await Record.ExceptionAsync(() =>
            service.RevokePointsManuallyAsync(UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None));

        Assert.Null(ex);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Grant_ExistingKeyRow_ShortCircuits_WithoutGrantingOrFlushing()
    {
        var account = ArrangeAccount();
        var existing = LoyaltyTransaction.Create(
            account.Id, LoyaltyTransactionType.Earn, Points, LoyaltyEarnSource.ManualGrant, null,
            idempotencyKey: RequestId);
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateService().GrantPointsManuallyAsync(
            UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);

        Assert.Empty(account.Transactions);
        Assert.Equal(0, account.LifetimePoints);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}
