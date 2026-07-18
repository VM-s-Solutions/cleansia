using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// Proportional loyalty clawback on a partial refund. The method is keyed per refund, so two distinct
/// partial refunds each revoke — it is not the one-shot cancel mirror that no-ops on a second call.
/// Σ(revoked) across an order's partials is capped at the original OrderCompleted earn.
///
/// These are logic-level unit tests with mocked repositories: the fast-path key lookup is the mocked
/// GetByIdempotencyKeyAsync and the concurrent-race backstop is the mocked CommitAsync throwing a wrapped
/// 23505. A true-parallel proof against a real filtered unique index belongs to the integration suite.
/// </summary>
public class PartialRefundLoyaltyClawbackTests
{
    private const string UserId = "user-1";
    private const string ActorId = "system";
    private const string OrderId = "order-1";
    private const string RefundKey = "refund-key-aaa";
    private const string OtherRefundKey = "refund-key-bbb";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();

    private LoyaltyService CreateService() =>
        new(
            _orderRepository.Object,
            _accountRepository.Object,
            _tierConfigRepository.Object,
            _transactionRepository.Object,
            _producer.Object,
            NullLogger<LoyaltyService>.Instance);

    private LoyaltyAccount ArrangeAccount(int originalEarn)
    {
        var account = LoyaltyAccount.Create(UserId);
        account.Id = "acct-1";
        account.GrantPoints(originalEarn, LoyaltyEarnSource.OrderCompleted, OrderId, ActorId, DefaultThresholds());
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return account;
    }

    private void ArrangeOrder(string? userId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 1000m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Paid,
            userId: userId);
        order.Id = OrderId;

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
    }

    private void ArrangeOriginalEarn(int earn)
    {
        var earnRow = LoyaltyTransaction.Create(
            "acct-1", LoyaltyTransactionType.Earn, earn, LoyaltyEarnSource.OrderCompleted, OrderId);
        _transactionRepository
            .Setup(r => r.GetLatestForOrderSourceAsync(OrderId, LoyaltyEarnSource.OrderCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(earnRow);
    }

    private void ArrangeNoExistingKey()
    {
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyTransaction?)null);
    }

    private void ArrangeAlreadyRevoked(int alreadyRevoked)
    {
        _transactionRepository
            .Setup(r => r.GetRevokedPointsSumForOrderSourceAsync(
                OrderId, LoyaltyEarnSource.OrderPartiallyRefunded, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alreadyRevoked);
    }

    private void ArrangeCommit() =>
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

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

    private static IReadOnlyList<LoyaltyTransaction> PartialRevokes(LoyaltyAccount account) =>
        account.Transactions
            .Where(t => t.Source == LoyaltyEarnSource.OrderPartiallyRefunded)
            .ToList();

    [Fact]
    public async Task PartialRevoke_RevokesFloorOfRefundNetOverTen()
    {
        var account = ArrangeAccount(originalEarn: 100);
        ArrangeOrder(UserId);
        ArrangeOriginalEarn(1000);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();
        ArrangeCommit();

        await CreateService().RevokeForPartialRefundAsync(OrderId, 95m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-9, revoke.Points);
        Assert.Equal(RefundKey, revoke.IdempotencyKey);
    }

    [Fact]
    public async Task PartialRevoke_SameRefundKeyTwice_RevokesOnce()
    {
        var account = ArrangeAccount(originalEarn: 100);
        ArrangeOrder(UserId);
        ArrangeOriginalEarn(1000);
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();

        LoyaltyTransaction? existing = null;
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RefundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existing);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => existing ??= account.Transactions.LastOrDefault(t => t.IdempotencyKey == RefundKey))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.RevokeForPartialRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None);
        await service.RevokeForPartialRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-10, revoke.Points);
    }

    [Fact]
    public async Task PartialRevoke_TwoDifferentRefunds_EachRevokes()
    {
        var account = ArrangeAccount(originalEarn: 100);
        ArrangeOrder(UserId);
        ArrangeOriginalEarn(1000);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        ArrangeCommit();

        // Each refund sees the running total revoked by the previous one.
        var revokedSoFar = 0;
        _transactionRepository
            .Setup(r => r.GetRevokedPointsSumForOrderSourceAsync(
                OrderId, LoyaltyEarnSource.OrderPartiallyRefunded, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => revokedSoFar);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => revokedSoFar = PartialRevokes(account).Sum(t => -t.Points))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.RevokeForPartialRefundAsync(OrderId, 30m, RefundKey, ActorId, CancellationToken.None);
        await service.RevokeForPartialRefundAsync(OrderId, 50m, OtherRefundKey, ActorId, CancellationToken.None);

        var revokes = PartialRevokes(account);
        Assert.Equal(2, revokes.Count);
        Assert.Equal(8, revokes.Sum(t => -t.Points));
    }

    [Fact]
    public async Task PartialRevoke_CumulativeCap_NeverExceedsOriginalEarn()
    {
        var account = ArrangeAccount(originalEarn: 100);
        ArrangeOrder(UserId);
        ArrangeOriginalEarn(100);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();

        // 90 already revoked under prior partials; the next refund asks for floor(500/10)=50, but only 10
        // of headroom remains to the original earn.
        ArrangeAlreadyRevoked(90);
        ArrangeCommit();

        await CreateService().RevokeForPartialRefundAsync(OrderId, 500m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-10, revoke.Points);
    }

    [Fact]
    public async Task PartialRevoke_NoHeadroomLeft_NoOps()
    {
        var account = ArrangeAccount(originalEarn: 100);
        ArrangeOrder(UserId);
        ArrangeOriginalEarn(100);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        ArrangeAlreadyRevoked(100);

        await CreateService().RevokeForPartialRefundAsync(OrderId, 500m, RefundKey, ActorId, CancellationToken.None);

        Assert.Empty(PartialRevokes(account));
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartialRevoke_AnonymousOrder_NoOps()
    {
        ArrangeOrder(userId: null);

        await CreateService().RevokeForPartialRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None);

        _accountRepository.Verify(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartialRevoke_Concurrent_SameRefundKey_UniqueViolation_Collapses_NoThrow()
    {
        ArrangeAccount(originalEarn: 100);
        ArrangeOrder(UserId);
        ArrangeOriginalEarn(1000);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();

        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var ex = await Record.ExceptionAsync(() =>
            CreateService().RevokeForPartialRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None));

        Assert.Null(ex);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionRepository.Verify(r => r.Rollback(), Times.Once);
    }

    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}
