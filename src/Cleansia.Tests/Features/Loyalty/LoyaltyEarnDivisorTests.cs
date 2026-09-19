using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// A completed order earns <c>floor(total / Currency.LoyaltyPointsDivisor)</c> -- the divisor is
/// AUTHORED per currency, like a price. The hard-coded <c>/ 10</c> it replaces meant a EUR order earned
/// 1/25 of the points a CZK order earned for the same spend, and tiers were unreachable in that market.
/// A currency with no divisor earns nothing and logs: switched on before its rate was authored, it must
/// not earn at another currency's rate in either direction (T-0703).
/// </summary>
public class LoyaltyEarnDivisorTests
{
    private const string UserId = "user-1";
    private const string OrderId = "order-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();
    private readonly Mock<INotificationProducer> _producer = new();
    private readonly List<(LogLevel Level, string Message)> _log = [];

    private readonly LoyaltyAccount _account = LoyaltyAccount.Create(UserId);

    public LoyaltyEarnDivisorTests()
    {
        _account.Id = "acct-1";
        _accountRepository
            .Setup(r => r.EnsureForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_account);
        _transactionRepository
            .Setup(r => r.GetLatestForOrderSourceAsync(OrderId, LoyaltyEarnSource.OrderCompleted, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyTransaction?)null);
        _tierConfigRepository
            .Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, 500, 0m, null, "[]"),
                LoyaltyTierConfig.Create(LoyaltyTier.GoldPolisher, 2000, 0m, null, "[]"),
                LoyaltyTierConfig.Create(LoyaltyTier.PlatinumSparkler, 5000, 0m, null, "[]"),
            ]);
    }

    [Fact]
    public async Task Earn_Uses_The_Order_Currencys_Divisor()
    {
        ArrangeOrder(total: 40.00m, currencyId: "eur");
        ArrangeCurrency("eur", divisor: 0.40m);

        await CreateService().GrantForCompletedOrderAsync(OrderId, CancellationToken.None);

        Assert.Equal(100, _account.LifetimePoints);
    }

    [Fact]
    public async Task Czk_At_Ten_Reproduces_The_Historical_Rate()
    {
        ArrangeOrder(total: 1000.00m, currencyId: "czk");
        ArrangeCurrency("czk", divisor: 10m);

        await CreateService().GrantForCompletedOrderAsync(OrderId, CancellationToken.None);

        Assert.Equal(100, _account.LifetimePoints);
    }

    [Fact]
    public async Task Earn_Skips_And_Logs_When_The_Currency_Has_No_Divisor()
    {
        ArrangeOrder(total: 1000.00m, currencyId: "eur");
        ArrangeCurrency("eur", divisor: null);

        await CreateService().GrantForCompletedOrderAsync(OrderId, CancellationToken.None);

        Assert.Equal(0, _account.LifetimePoints);
        _accountRepository.Verify(r => r.EnsureForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(_log, e => e.Level == LogLevel.Warning && e.Message.Contains("eur"));
    }

    // ── arrangement ──────────────────────────────────────────────────

    private LoyaltyService CreateService() =>
        new(
            _orderRepository.Object,
            _accountRepository.Object,
            _tierConfigRepository.Object,
            _transactionRepository.Object,
            _currencyRepository.Object,
            _producer.Object,
            new CapturingLogger(_log));

    private void ArrangeOrder(decimal total, string currencyId)
    {
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Cash,
            totalPrice: total,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Paid,
            userId: UserId);
        order.Id = OrderId;
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());
    }

    private void ArrangeCurrency(string id, decimal? divisor)
    {
        var currency = Currency.Create(id.ToUpperInvariant(), "¤", id);
        currency.Id = id;
        currency.SetLoyaltyPointsDivisor(divisor);
        _currencyRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
    }

    private sealed class CapturingLogger(List<(LogLevel Level, string Message)> entries) : ILogger<LoyaltyService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
