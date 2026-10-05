using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.TestUtilities;
using Cleansia.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// A card order refunded before the job ends is completed by its cleaner like any other: the crew's pay is
/// asked for, and the customer earns on the whole price with the share of what was given back taken at once.
/// It used to be refused at completion as unconfirmed, so the earn and the early-refund clawback were never
/// written. Every case is a 1000 CZK order at a divisor of 10, so it earns 100 points.
/// </summary>
public class CompleteEarlyRefundedCardOrderTests
{
    private const string OrderId = "order-early-refund";
    private const string EmployeeId = "emp-early-refund";
    private const string UserId = "user-early-refund";

    private static readonly LoyaltyTierThresholds Thresholds = new(Silver: 500, Gold: 2000, Platinum: 5000);

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IPendingDispatch> _pending = new();
    private readonly Mock<ILoyaltyAccountRepository> _accounts = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactions = new();
    private readonly Mock<IRefundRepository> _refunds = new();
    private readonly LoyaltyAccount _account = LoyaltyAccount.Create(UserId);

    [Theory]
    [InlineData(PaymentStatus.PartiallyRefunded, 200, -20, 80)] // floor(100 × 200 / 1000) = 20
    [InlineData(PaymentStatus.Refunded, 1000, -100, 0)] // the whole price back: the earn nets to nothing
    public async Task The_Cleaner_Completes_It_The_Crew_Pay_Is_Asked_For_And_The_Early_Refund_Takes_Its_Share(
        PaymentStatus paymentStatus, int refundedBeforeTheJobEnded, int earlyRefundRow, int pointsKept)
    {
        ArrangeCardOrder(paymentStatus, refundedBeforeTheJobEnded);
        var command = new CompleteOrder.Command(OrderId, ActualCompletionTimeMinutes: 120);

        var validation = await Validator().ValidateAsync(command);
        Assert.True(validation.IsValid, string.Join(", ", validation.Errors.Select(e => e.ErrorMessage)));
        var result = await Handler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(100, Assert.Single(Ledger(LoyaltyEarnSource.OrderCompleted)).Points);
        Assert.Equal(earlyRefundRow, Assert.Single(Ledger(LoyaltyEarnSource.OrderPartiallyRefunded)).Points);
        Assert.Equal(pointsKept, _account.LifetimePoints);
        _pending.Verify(
            p => p.Enqueue(
                QueueNames.CalculateOrderPay,
                It.IsAny<QueueEnvelope<CalculateOrderPayMessage>>(),
                MessageKeys.Pay(OrderId, EmployeeId)),
            Times.Once);
    }

    private void ArrangeCardOrder(PaymentStatus paymentStatus, decimal refundedBeforeTheJobEnded)
    {
        var order = ValidatorTestHelpers.BuildOrder(
            OrderId, OrderStatus.InProgress, EmployeeId, PaymentType.Card, paymentStatus, userId: UserId);
        _orders.Setup(r => r.GetQueryable()).Returns(() => new[] { order }.AsQueryable().BuildMock());
        _orders.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        _account.Id = "acct-early-refund";
        _accounts.Setup(r => r.EnsureForUserAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
        _transactions
            .Setup(r => r.GetLatestForOrderSourceAsync(
                It.IsAny<string>(), It.IsAny<LoyaltyEarnSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string orderId, LoyaltyEarnSource source, CancellationToken _) =>
                _account.Transactions.LastOrDefault(t => t.OrderId == orderId && t.Source == source));
        _refunds
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(refundedBeforeTheJobEnded);
    }

    // The assigned, approved cleaner with a profile, a signed contract for the seat and an after photo.
    private CompleteOrder.Validator Validator()
    {
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, ContractStatus.Approved, withAddress: true);
        var employees = new Mock<IEmployeeRepository>();
        employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        employees.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());
        var photos = new Mock<IOrderPhotoRepository>();
        photos
            .Setup(r => r.GetPhotoCountByOrderIdAndTypeAsync(OrderId, PhotoType.After, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        var access = new Mock<IOrderAccessService>();
        access.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);

        return new CompleteOrder.Validator(
            _orders.Object,
            employees.Object,
            photos.Object,
            access.Object,
            WorkContractTestData.AcceptanceRepository().Object);
    }

    private CompleteOrder.Handler Handler() =>
        new(
            _orders.Object,
            _pending.Object,
            Mock.Of<INotificationProducer>(),
            Mock.Of<ILiveActivityProducer>(),
            Mock.Of<IEmailService>(),
            TestGuestOrderAccessTokenIssuer.WithNoLiveTokens(),
            Loyalty(),
            Mock.Of<IReferralService>(),
            NullLogger<CompleteOrder.Handler>.Instance);

    private LoyaltyService Loyalty()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        currency.Id = ValidatorTestHelpers.CurrencyId;
        currency.SetLoyaltyPointsDivisor(10m);
        var currencies = new Mock<ICurrencyRepository>();
        currencies
            .Setup(r => r.GetByIdAsync(ValidatorTestHelpers.CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        var tierConfigs = new Mock<ILoyaltyTierConfigRepository>();
        tierConfigs
            .Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, Thresholds.Silver, 0m, null, "[]"),
                LoyaltyTierConfig.Create(LoyaltyTier.GoldPolisher, Thresholds.Gold, 0m, null, "[]"),
                LoyaltyTierConfig.Create(LoyaltyTier.PlatinumSparkler, Thresholds.Platinum, 0m, null, "[]"),
            ]);

        return new LoyaltyService(
            _orders.Object,
            _accounts.Object,
            tierConfigs.Object,
            _transactions.Object,
            currencies.Object,
            _refunds.Object,
            Mock.Of<ICreditAccountRepository>(),
            Mock.Of<INotificationProducer>(),
            NullLogger<LoyaltyService>.Instance);
    }

    private IReadOnlyList<LoyaltyTransaction> Ledger(LoyaltyEarnSource source) =>
        _account.Transactions.Where(t => t.OrderId == OrderId && t.Source == source).ToList();
}
