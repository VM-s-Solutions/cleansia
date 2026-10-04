using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// The one loyalty clawback every refund of a completed order goes through — partial, full and a
/// dispute's settlement: a refund takes back the same share of the points the order's OrderCompleted earn
/// granted as it returned of the order's price. The method is keyed per refund, so two distinct refunds
/// each revoke — it is not the one-shot cancel mirror that no-ops on a second call. Σ(revoked) across an
/// order's refunds is capped at the original earn, so a full refund takes only what the others left.
///
/// These are logic-level unit tests with mocked repositories: the fast-path key lookup is the mocked
/// GetByIdempotencyKeyAsync and the concurrent-race backstop is the mocked CommitAsync throwing a wrapped
/// 23505. A true-parallel proof against a real filtered unique index belongs to the integration suite.
/// </summary>
public class RefundLoyaltyClawbackTests
{
    private const string UserId = "user-1";
    private const string ActorId = "system";
    private const string OrderId = "order-1";
    private const string RefundKey = "refund-key-aaa";
    private const string OtherRefundKey = "refund-key-bbb";
    private const string FullRefundKey = "refund:order-1:admin:full";
    private const string DisputeSettlementKey = "dispute-settlement:dispute-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    private LoyaltyService CreateService() =>
        new(
            _orderRepository.Object,
            _accountRepository.Object,
            _tierConfigRepository.Object,
            _transactionRepository.Object,
            _currencyRepository.Object,
            Mock.Of<IRefundRepository>(),
            Mock.Of<ICreditAccountRepository>(),
            _producer.Object,
            NullLogger<LoyaltyService>.Instance);

    /// <summary>
    /// The account and the OrderCompleted ledger row the repository returns are the same grant, so the
    /// earn the clawback reads is the earn the account holds.
    /// </summary>
    private LoyaltyAccount ArrangeEarn(int points)
    {
        var account = LoyaltyAccount.Create(UserId);
        account.Id = "acct-1";
        account.GrantPoints(points, LoyaltyEarnSource.OrderCompleted, OrderId, ActorId, DefaultThresholds());
        var earnRow = account.Transactions.Single(t => t.Source == LoyaltyEarnSource.OrderCompleted);

        _accountRepository
            .Setup(r => r.GetByUserIdIgnoringTenantAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _transactionRepository
            .Setup(r => r.GetLatestForOrderSourceAsync(OrderId, LoyaltyEarnSource.OrderCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync(earnRow);
        return account;
    }

    private void ArrangeOrder(string? userId, decimal totalPrice = 1000m)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: totalPrice,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Paid,
            userId: userId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;

        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
    }

    private void ArrangeDivisor(decimal? divisor)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        currency.Id = "currency-1";
        currency.SetLoyaltyPointsDivisor(divisor);
        _currencyRepository
            .Setup(r => r.GetByIdAsync("currency-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
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

    /// <summary>Each refund sees the running total revoked by the ones before it, as the ledger would.</summary>
    private void ArrangeRunningRevokedTotal(LoyaltyAccount account)
    {
        var revokedSoFar = 0;
        _transactionRepository
            .Setup(r => r.GetRevokedPointsSumForOrderSourceAsync(
                OrderId, LoyaltyEarnSource.OrderPartiallyRefunded, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => revokedSoFar);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => revokedSoFar = PartialRevokes(account).Sum(t => -t.Points))
            .Returns(Task.CompletedTask);
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
    public async Task PartialRevoke_RemovesTheReturnedShareOfTheEarn_Floored()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId, totalPrice: 1000m);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();
        ArrangeCommit();

        // floor(100 × 95 / 1000) = floor(9.5) = 9.
        await CreateService().RevokeForRefundAsync(OrderId, 95m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-9, revoke.Points);
        Assert.Equal(RefundKey, revoke.IdempotencyKey);
    }

    /// <summary>
    /// The clawback follows what the order EARNED, not the currency's rate on the day of the refund. A
    /// 2000 order earned 200 points at a divisor of 10; whether the admin has since lowered the divisor
    /// to 5, raised it to 20 or cleared it, refunding half the order takes back half the earn. Dividing
    /// the refund by the live divisor took 200, 50 and 0.
    /// </summary>
    [Theory]
    [InlineData(5.0)]
    [InlineData(20.0)]
    [InlineData(null)]
    public async Task PartialRevoke_DivisorChangedSinceTheEarn_DoesNotChangeTheClawback(double? divisorNow)
    {
        var account = ArrangeEarn(200);
        ArrangeOrder(UserId, totalPrice: 2000m);
        ArrangeDivisor((decimal?)divisorNow);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();
        ArrangeCommit();

        await CreateService().RevokeForRefundAsync(OrderId, 1000m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-100, revoke.Points);
        _currencyRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Returning the whole price takes back the whole earn. The earn is on the gross price, so a 1210
    /// order with 21 % VAT earned 121; the clawback on the VAT-stripped 1000 took 100 and left the
    /// customer 21 points for money they got back.
    /// </summary>
    [Fact]
    public async Task PartialRevoke_WholePriceReturned_RemovesEveryEarnedPoint()
    {
        var account = ArrangeEarn(121);
        ArrangeOrder(UserId, totalPrice: 1210m);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();
        ArrangeCommit();

        await CreateService().RevokeForRefundAsync(OrderId, 1210m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-121, revoke.Points);
        Assert.Equal(0, account.LifetimePoints);
    }

    /// <summary>An order that earned nothing — its currency had no divisor at completion — gives nothing back.</summary>
    [Fact]
    public async Task PartialRevoke_OrderEarnedNothing_NoOps()
    {
        ArrangeOrder(UserId);
        _transactionRepository
            .Setup(r => r.GetLatestForOrderSourceAsync(OrderId, LoyaltyEarnSource.OrderCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyTransaction?)null);
        ArrangeNoExistingKey();

        await CreateService().RevokeForRefundAsync(OrderId, 500m, RefundKey, ActorId, CancellationToken.None);

        _accountRepository.Verify(r => r.GetByUserIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartialRevoke_ZeroPriceOrder_NoOps()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId, totalPrice: 0m);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();
        ArrangeCommit();

        var ex = await Record.ExceptionAsync(() =>
            CreateService().RevokeForRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None));

        Assert.Null(ex);
        Assert.Empty(PartialRevokes(account));
    }

    [Fact]
    public async Task PartialRevoke_SameRefundKeyTwice_RevokesOnce()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId);
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
        await service.RevokeForRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None);
        await service.RevokeForRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-10, revoke.Points);
    }

    [Fact]
    public async Task PartialRevoke_TwoDifferentRefunds_EachRevokes()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        ArrangeRunningRevokedTotal(account);

        var service = CreateService();
        await service.RevokeForRefundAsync(OrderId, 30m, RefundKey, ActorId, CancellationToken.None);
        await service.RevokeForRefundAsync(OrderId, 50m, OtherRefundKey, ActorId, CancellationToken.None);

        var revokes = PartialRevokes(account);
        Assert.Equal(2, revokes.Count);
        Assert.Equal(8, revokes.Sum(t => -t.Points));
    }

    /// <summary>
    /// Two refunds whose shares add up to more than the earn stop at the earn: 600 of a 1000 order asks
    /// for 60 of its 100 points, and a second 600 asks for 60 more but finds only 40 left.
    /// </summary>
    [Fact]
    public async Task PartialRevoke_TwoRefunds_NeverTakeBackMoreThanTheEarn()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId, totalPrice: 1000m);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        ArrangeRunningRevokedTotal(account);

        var service = CreateService();
        await service.RevokeForRefundAsync(OrderId, 600m, RefundKey, ActorId, CancellationToken.None);
        await service.RevokeForRefundAsync(OrderId, 600m, OtherRefundKey, ActorId, CancellationToken.None);

        Assert.Equal([-60, -40], PartialRevokes(account).Select(t => t.Points));
        Assert.Equal(0, account.LifetimePoints);
    }

    /// <summary>
    /// A 1000 order earned 99. A partial refund of 300 takes floor(99 × 300 / 1000) = floor(29.7) = 29.
    /// The full refund then asks for the whole price — floor(99 × 1000 / 1000) = 99 — and finds 70 left,
    /// so it takes 70, not the 69 the remaining 700 would floor to on its own: nothing is left behind.
    /// </summary>
    [Fact]
    public async Task FullRefund_AfterAPartialRefund_TakesOnlyWhatIsLeft()
    {
        var account = ArrangeEarn(99);
        ArrangeOrder(UserId, totalPrice: 1000m);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        ArrangeRunningRevokedTotal(account);

        var service = CreateService();
        await service.RevokeForRefundAsync(OrderId, 300m, RefundKey, ActorId, CancellationToken.None);
        await service.RevokeForRefundAsync(OrderId, 1000m, FullRefundKey, ActorId, CancellationToken.None);

        Assert.Equal([-29, -70], PartialRevokes(account).Select(t => t.Points));
        Assert.Equal([RefundKey, FullRefundKey], PartialRevokes(account).Select(t => t.IdempotencyKey));
        Assert.Equal(0, account.LifetimePoints);
    }

    /// <summary>
    /// A dispute on a 1000 order that earned 100 is settled with 300 to the card and 160 back to the
    /// credit balance: it returned 460, so it takes floor(100 × 460 / 1000) = 46 and leaves 54.
    /// </summary>
    [Fact]
    public async Task DisputeRefund_TakesTheShareItReturned_CardPlusCredit()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId, totalPrice: 1000m);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();
        ArrangeCommit();

        await CreateService().RevokeForRefundAsync(OrderId, 300m + 160m, DisputeSettlementKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-46, revoke.Points);
        Assert.Equal(DisputeSettlementKey, revoke.IdempotencyKey);
        Assert.Equal(54, account.LifetimePoints);
    }

    /// <summary>
    /// The full refund retried on its own key takes the points once: 100 earned, 100 taken, and the
    /// replay finds its key already on the ledger.
    /// </summary>
    [Fact]
    public async Task FullRefund_Retried_TakesThePointsOnce()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId, totalPrice: 1000m);
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();

        LoyaltyTransaction? existing = null;
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(FullRefundKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existing);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => existing ??= account.Transactions.LastOrDefault(t => t.IdempotencyKey == FullRefundKey))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.RevokeForRefundAsync(OrderId, 1000m, FullRefundKey, ActorId, CancellationToken.None);
        await service.RevokeForRefundAsync(OrderId, 1000m, FullRefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-100, revoke.Points);
        Assert.Equal(0, account.LifetimePoints);
    }

    [Fact]
    public async Task FullRefund_OrderEarnedNothing_TakesNothing()
    {
        ArrangeOrder(UserId, totalPrice: 1000m);
        _transactionRepository
            .Setup(r => r.GetLatestForOrderSourceAsync(OrderId, LoyaltyEarnSource.OrderCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyTransaction?)null);
        ArrangeNoExistingKey();

        await CreateService().RevokeForRefundAsync(OrderId, 1000m, FullRefundKey, ActorId, CancellationToken.None);

        _accountRepository.Verify(r => r.GetByUserIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartialRevoke_CumulativeCap_NeverExceedsOriginalEarn()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();

        // 90 already revoked under prior partials; the next refund returns half the order and asks for
        // half the earn, 50, but only 10 of headroom remains.
        ArrangeAlreadyRevoked(90);
        ArrangeCommit();

        await CreateService().RevokeForRefundAsync(OrderId, 500m, RefundKey, ActorId, CancellationToken.None);

        var revoke = Assert.Single(PartialRevokes(account));
        Assert.Equal(-10, revoke.Points);
    }

    [Fact]
    public async Task PartialRevoke_NoHeadroomLeft_NoOps()
    {
        var account = ArrangeEarn(100);
        ArrangeOrder(UserId);
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        ArrangeAlreadyRevoked(100);

        await CreateService().RevokeForRefundAsync(OrderId, 500m, RefundKey, ActorId, CancellationToken.None);

        Assert.Empty(PartialRevokes(account));
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartialRevoke_AnonymousOrder_NoOps()
    {
        ArrangeOrder(userId: null);

        await CreateService().RevokeForRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None);

        _accountRepository.Verify(r => r.GetByUserIdIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartialRevoke_Concurrent_SameRefundKey_UniqueViolation_Collapses_NoThrow()
    {
        ArrangeEarn(100);
        ArrangeOrder(UserId);
        ArrangeNoExistingKey();
        ArrangeAlreadyRevoked(0);
        ArrangeTierConfigs();

        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var ex = await Record.ExceptionAsync(() =>
            CreateService().RevokeForRefundAsync(OrderId, 100m, RefundKey, ActorId, CancellationToken.None));

        Assert.Null(ex);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _transactionRepository.Verify(r => r.Rollback(), Times.Once);
    }

    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}
