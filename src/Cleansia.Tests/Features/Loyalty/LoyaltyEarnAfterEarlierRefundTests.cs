using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// Money an order gives back before it completes — a dispute settled in credit, a card refund and the
/// credit leg returned with it — takes back its share of the earn at completion, by the clawback's own
/// rule: <c>floor(earn × returned / TotalPrice)</c>. Before this, completion earned on the whole price and
/// the refund's own clawback had found no earn to take from, so the money came back and the points stayed.
///
/// <para>Every case is a 1000 CZK order at a divisor of 10, so it earns 100 points.</para>
/// </summary>
public class LoyaltyEarnAfterEarlierRefundTests
{
    private const string UserId = "user-1";
    private const string OrderId = "order-1";
    private const string CurrencyId = "czk";

    [Fact]
    public async Task A_Dispute_Settled_In_Credit_Before_Completion_Takes_Its_Share_At_Completion()
    {
        var harness = new Harness { DisputeSettledInCredit = 200m };

        await harness.CompleteAsync();

        // floor(100 × 200 / 1000) = 20 of the 100 earned.
        Assert.Equal(100, harness.Ledger(LoyaltyEarnSource.OrderCompleted).Single().Points);
        Assert.Equal(-20, harness.Ledger(LoyaltyEarnSource.OrderPartiallyRefunded).Single().Points);
        Assert.Equal(80, harness.Account.LifetimePoints);
    }

    [Fact]
    public async Task Card_Refunds_Their_Credit_Legs_And_Dispute_Credit_All_Count()
    {
        var harness = new Harness { CardRefunded = 150m, CreditReturned = 50m, DisputeSettledInCredit = 100m };

        await harness.CompleteAsync();

        // 150 + 50 + 100 = 300 returned: floor(100 × 300 / 1000) = 30.
        Assert.Equal(70, harness.Account.LifetimePoints);
    }

    [Fact]
    public async Task The_Share_Is_Floored_As_The_Clawback_Floors_It()
    {
        var harness = new Harness { DisputeSettledInCredit = 255m };

        await harness.CompleteAsync();

        // floor(100 × 255 / 1000) = floor(25.5) = 25 taken back; the customer keeps the half point.
        Assert.Equal(75, harness.Account.LifetimePoints);
    }

    [Fact]
    public async Task Nothing_Returned_Writes_The_Earn_Alone()
    {
        var harness = new Harness();

        await harness.CompleteAsync();

        Assert.Equal(100, harness.Account.LifetimePoints);
        Assert.Empty(harness.Ledger(LoyaltyEarnSource.OrderPartiallyRefunded));
    }

    [Fact]
    public async Task Everything_Returned_Before_Completion_Leaves_Nothing_And_A_Later_Full_Refund_Takes_Nothing_More()
    {
        var harness = new Harness { CardRefunded = 1000m };

        await harness.CompleteAsync();

        // floor(100 × 1000 / 1000) = 100 taken back at completion.
        Assert.Equal(0, harness.Account.LifetimePoints);

        await harness.RefundAfterCompletionAsync(1000m, $"refund:{OrderId}:admin:full");

        // The full refund's cap leaves it 100 − 100 = 0 to take.
        Assert.Equal(0, harness.Account.LifetimePoints);
        Assert.Equal(-100, harness.Ledger(LoyaltyEarnSource.OrderPartiallyRefunded).Single().Points);
    }

    /// <summary>
    /// The timing of a refund no longer decides what it takes back. With whole shares the split also takes
    /// exactly what one refund of the sum would.
    /// </summary>
    [Theory]
    [InlineData(200, 300, 50)] // 20 + 30 = 50 = floor(100 × 500 / 1000)
    [InlineData(150, 350, 50)] // 15 + 35 = 50
    [InlineData(500, 500, 0)] // 50 + 50 = 100, the whole earn
    public async Task A_Refund_Before_Completion_And_One_After_Take_Back_What_One_Refund_Of_The_Sum_Would(
        int before, int after, int kept)
    {
        var split = new Harness { DisputeSettledInCredit = before };
        await split.CompleteAsync();
        await split.RefundAfterCompletionAsync(after, "refund-after");

        var oneOfTheSum = new Harness();
        await oneOfTheSum.CompleteAsync();
        await oneOfTheSum.RefundAfterCompletionAsync(before + after, "refund-sum");

        Assert.Equal(kept, split.Account.LifetimePoints);
        Assert.Equal(oneOfTheSum.Account.LifetimePoints, split.Account.LifetimePoints);
    }

    /// <summary>
    /// With fractional shares each refund is floored on its own, before completion as after it: 255 then
    /// 245 keeps 100 − 25 − 24 = 51, the same two refunds after completion keep 51, and one refund of 500
    /// would keep 50. The per-refund floor is the existing clawback rule, unchanged here.
    /// </summary>
    [Fact]
    public async Task With_Fractional_Shares_The_Split_Takes_Back_What_The_Same_Two_Refunds_Would_After_Completion()
    {
        var split = new Harness { DisputeSettledInCredit = 255m };
        await split.CompleteAsync();
        await split.RefundAfterCompletionAsync(245m, "refund-after");

        var bothAfter = new Harness();
        await bothAfter.CompleteAsync();
        await bothAfter.RefundAfterCompletionAsync(255m, "refund-before");
        await bothAfter.RefundAfterCompletionAsync(245m, "refund-after");

        Assert.Equal(51, split.Account.LifetimePoints);
        Assert.Equal(bothAfter.Account.LifetimePoints, split.Account.LifetimePoints);
    }

    /// <summary>
    /// The same line selection submitted again after completion resolves to the refund that settled before
    /// it, and its clawback runs again under that refund's key. Completion already took that refund's 20, so
    /// the replay takes nothing more.
    /// </summary>
    [Fact]
    public async Task A_Refund_Settled_Before_Completion_And_Replayed_After_It_Takes_Its_Share_Once()
    {
        const string refundKey = $"refund:{OrderId}:admin:lines";
        var harness = new Harness { CardRefunded = 200m };
        harness.RefundSettledOn(refundKey, DateTimeOffset.UtcNow.AddMinutes(-5));

        await harness.CompleteAsync();
        await harness.RefundAfterCompletionAsync(200m, refundKey);

        Assert.Equal(80, harness.Account.LifetimePoints);
        Assert.Equal(-20, harness.Ledger(LoyaltyEarnSource.OrderPartiallyRefunded).Single().Points);
    }

    [Fact]
    public async Task A_Refund_Settled_After_Completion_Takes_Its_Share()
    {
        const string refundKey = $"refund:{OrderId}:admin:lines";
        var harness = new Harness();

        await harness.CompleteAsync();
        harness.RefundSettledOn(refundKey, DateTimeOffset.UtcNow.AddMinutes(5));
        await harness.RefundAfterCompletionAsync(200m, refundKey);

        Assert.Equal(80, harness.Account.LifetimePoints);
    }

    [Fact]
    public async Task A_Gross_Earn_Past_A_Threshold_The_Net_Does_Not_Reach_Announces_No_Promotion()
    {
        var harness = new Harness(startingPoints: 450) { DisputeSettledInCredit = 600m };

        await harness.CompleteAsync();
        await harness.RunAfterSaveAsync(tierBefore: LoyaltyTier.BronzeCleaner);

        // 450 + 100 − floor(100 × 600 / 1000) = 490, short of Silver's 500.
        Assert.Equal(490, harness.Account.LifetimePoints);
        Assert.Equal(LoyaltyTier.BronzeCleaner, harness.Account.CurrentTier);
        harness.Producer.Verify(p => p.NotifyAsync(
                It.IsAny<string>(),
                NotificationEventCatalog.LoyaltyTierUpgrade,
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class Harness
    {
        private static readonly LoyaltyTierThresholds Thresholds = new(Silver: 500, Gold: 2000, Platinum: 5000);

        private readonly Mock<IOrderRepository> _orders = new();
        private readonly Mock<ILoyaltyAccountRepository> _accounts = new();
        private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigs = new();
        private readonly Mock<ILoyaltyTransactionRepository> _transactions = new();
        private readonly Mock<ICurrencyRepository> _currencies = new();
        private readonly Mock<IRefundRepository> _refunds = new();
        private readonly Mock<ICreditAccountRepository> _credit = new();
        private readonly List<Refund> _settledRefunds = [];
        private Func<LoyaltyTier?, CancellationToken, Task>? _afterSave;

        public Harness(int startingPoints = 0)
        {
            Account.Id = "acct-1";
            if (startingPoints > 0)
            {
                Account.GrantPoints(startingPoints, LoyaltyEarnSource.ManualGrant, null, "admin-1", Thresholds);
            }

            var order = Order.Create(
                customerName: "Test Customer",
                customerEmail: "customer@example.com",
                customerPhone: "+420123456789",
                customerAddress: null!,
                rooms: 2,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(-1),
                paymentType: PaymentType.Cash,
                totalPrice: 1000m,
                currencyId: CurrencyId,
                paymentStatus: PaymentStatus.Paid,
                userId: UserId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            _orders.Setup(r => r.GetQueryable()).Returns(() => new[] { order }.AsQueryable().BuildMock());

            var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
            currency.Id = CurrencyId;
            currency.SetLoyaltyPointsDivisor(10m);
            _currencies.Setup(r => r.GetByIdAsync(CurrencyId, It.IsAny<CancellationToken>())).ReturnsAsync(currency);

            _tierConfigs
                .Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, 500, 0m, null, "[]"),
                    LoyaltyTierConfig.Create(LoyaltyTier.GoldPolisher, 2000, 0m, null, "[]"),
                    LoyaltyTierConfig.Create(LoyaltyTier.PlatinumSparkler, 5000, 0m, null, "[]"),
                ]);

            _accounts.Setup(r => r.EnsureForUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(Account);
            _accounts
                .Setup(r => r.GetByUserIdIgnoringTenantAsync(UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Account);
            _accounts
                .Setup(r => r.AfterSave(Account, It.IsAny<Func<LoyaltyTier?, CancellationToken, Task>>()))
                .Callback<LoyaltyAccount, Func<LoyaltyTier?, CancellationToken, Task>>((_, afterSave) => _afterSave = afterSave);

            _transactions
                .Setup(r => r.GetLatestForOrderSourceAsync(
                    It.IsAny<string>(), It.IsAny<LoyaltyEarnSource>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string orderId, LoyaltyEarnSource source, CancellationToken _) =>
                    Account.Transactions.LastOrDefault(t => t.OrderId == orderId && t.Source == source));
            _transactions
                .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) =>
                    Account.Transactions.FirstOrDefault(t => t.IdempotencyKey == key));
            _transactions
                .Setup(r => r.GetRevokedPointsSumForOrderSourceAsync(
                    It.IsAny<string>(), It.IsAny<LoyaltyEarnSource>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string orderId, LoyaltyEarnSource source, CancellationToken _) =>
                    -Account.Transactions.Where(t => t.OrderId == orderId && t.Source == source).Sum(t => t.Points));
            _transactions.Setup(r => r.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _refunds
                .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CardRefunded);
            _refunds
                .Setup(r => r.GetByRefundKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string key, CancellationToken _) => _settledRefunds.FirstOrDefault(r => r.RefundKey == key));
            _credit
                .Setup(r => r.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreditReturned);
            _credit
                .Setup(r => r.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => DisputeSettledInCredit);
        }

        public LoyaltyAccount Account { get; } = LoyaltyAccount.Create(UserId);

        public Mock<INotificationProducer> Producer { get; } = new();

        public decimal CardRefunded { get; init; }

        public decimal CreditReturned { get; init; }

        public decimal DisputeSettledInCredit { get; init; }

        public void RefundSettledOn(string refundKey, DateTimeOffset settledOn) =>
            _settledRefunds.Add(Refund
                .Create(OrderId, refundKey, 200m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund)
                .MarkSucceeded(stripeRefundId: null, confirmedOnUtc: settledOn));

        public Task CompleteAsync() =>
            Service().GrantForCompletedOrderAsync(OrderId, CancellationToken.None);

        public Task RefundAfterCompletionAsync(decimal amountReturned, string refundKey) =>
            Service().RevokeForRefundAsync(OrderId, amountReturned, refundKey, "admin-1", CancellationToken.None);

        public Task RunAfterSaveAsync(LoyaltyTier tierBefore) => _afterSave!(tierBefore, CancellationToken.None);

        public IReadOnlyList<LoyaltyTransaction> Ledger(LoyaltyEarnSource source) =>
            Account.Transactions.Where(t => t.OrderId == OrderId && t.Source == source).ToList();

        private LoyaltyService Service() =>
            new(
                _orders.Object,
                _accounts.Object,
                _tierConfigs.Object,
                _transactions.Object,
                _currencies.Object,
                _refunds.Object,
                _credit.Object,
                Producer.Object,
                NullLogger<LoyaltyService>.Instance);
    }
}
