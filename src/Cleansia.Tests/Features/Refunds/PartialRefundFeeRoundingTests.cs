using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// Adversarial rounding gap-fill for the admin partial-refund fee deduction (ADR-0009 D3). The
/// existing handler suite asserts a clean-integer fee (1.4% + 6 of 1000 = 20); these pin the HALF-CENT
/// rounding DIRECTION and the null-config = platform-absorbs boundary. The Stripe fee is
/// <c>round(refundAmount × rate/100 + fixedFee, 2, AwayFromZero)</c> — a banker's-rounding (HalfEven)
/// implementation would disagree on the <c>.xx5</c> cases below, which is exactly the bug these bite.
/// Every expected money value is hand-derived, never the production expression re-run in the test.
/// </summary>
public class PartialRefundFeeRoundingTests
{
    private const string OrderId = "order-1";
    private const string AdminId = "admin-1";
    private const string CountryId = "cz";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IRefundRepository> _refundRepository = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfigurationRepository = new();
    private readonly RecordingRefundService _refundService = new();
    private readonly RecordingLoyaltyService _loyaltyService = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public PartialRefundFeeRoundingTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(AdminId);
    }

    private readonly AuditContext _auditContext = new();

    private IssuePartialRefund.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            _refundRepository.Object,
            _countryConfigurationRepository.Object,
            _refundService,
            _loyaltyService,
            _session.Object,
            _auditContext,
            NullLogger<IssuePartialRefund.Handler>.Instance);

    private void ArrangeCountryFee(decimal? rate, decimal? fixedFee)
    {
        var config = CountryConfiguration.Create("cz", "CZK", "cs", 21m);
        config.UpdateRefundStripeFee(rate, fixedFee);
        _countryConfigurationRepository
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
    }

    private static Order SingleServiceOrder(decimal totalPrice, decimal? appliedVatRate = null)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var address = Address.Create("Street 1", "Prague", "11000", CountryId);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "user-1");
        order.Id = OrderId;
        order.SetCurrency(currency);
        order.SetVatBreakdown(
            netAmount: appliedVatRate is { } rate ? totalPrice * 100m / (100m + rate) : totalPrice,
            vatAmount: appliedVatRate is { } r ? totalPrice * r / (100m + r) : 0m,
            appliedRate: appliedVatRate);
        var svc = Service.Create("cat-1", "Service A", "", totalPrice, 0m);
        svc.Id = "svc-a";
        order.AddSelectedServices([OrderService.Create(order, svc)]);
        order.CompleteOrder(actualCompletionTime: 120);
        return order;
    }

    private void Arrange(Order order)
    {
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order.TotalPrice);
    }

    private static IssuePartialRefund.Command AdminDiscretionWholeOrder() =>
        new(
            OrderId,
            [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
            RefundReason.AdminDiscretion,
            OverrideReason: null);

    [Fact]
    public async Task Fee_AtHalfCent_RoundsAwayFromZero_NotBankers()
    {
        // refundAmount 10.00 (whole single line), rate 4.05%, fixed 0 → fee raw = 10 × 0.0405 = 0.405.
        // AwayFromZero → 0.41 (banker's HalfEven would give 0.40 and over-refund by a cent).
        var order = SingleServiceOrder(totalPrice: 10m);
        Arrange(order);
        ArrangeCountryFee(rate: 4.05m, fixedFee: 0m);

        var result = await CreateHandler().Handle(AdminDiscretionWholeOrder(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // sent = 10.00 − 0.41 = 9.59 (hand-derived).
        Assert.Equal(9.59m, _refundService.LastRequest!.Amount);
        Assert.Equal(9.59m, result.Value!.RefundAmount);
    }

    [Fact]
    public async Task Fee_WithFixedComponent_AtHalfCent_RoundsAwayFromZero()
    {
        // rate 1.25% of 200 = 2.50, fixed 0.005 → fee raw 2.505 → AwayFromZero 2.51 (banker's 2.50).
        var order = SingleServiceOrder(totalPrice: 200m);
        Arrange(order);
        ArrangeCountryFee(rate: 1.25m, fixedFee: 0.005m);

        var result = await CreateHandler().Handle(AdminDiscretionWholeOrder(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // sent = 200.00 − 2.51 = 197.49 (hand-derived).
        Assert.Equal(197.49m, _refundService.LastRequest!.Amount);
    }

    [Fact]
    public async Task Fee_NullRate_PlatformAbsorbs_FullAmountSent()
    {
        // Config exists but RefundStripeFeeRate is null → fee 0 (fail-open), full amount sent.
        var order = SingleServiceOrder(totalPrice: 1000m);
        Arrange(order);
        ArrangeCountryFee(rate: null, fixedFee: 6m);

        var result = await CreateHandler().Handle(AdminDiscretionWholeOrder(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, _refundService.LastRequest!.Amount);
    }

    [Fact]
    public async Task Fee_NullFixedFee_PlatformAbsorbs_FullAmountSent()
    {
        // Config exists with a rate but the fixed component is null → either-null means fee 0.
        var order = SingleServiceOrder(totalPrice: 1000m);
        Arrange(order);
        ArrangeCountryFee(rate: 1.4m, fixedFee: null);

        var result = await CreateHandler().Handle(AdminDiscretionWholeOrder(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, _refundService.LastRequest!.Amount);
    }

    [Fact]
    public async Task Fee_DeductedBeforeVatNetDerivation_VatOffConfirmedAmount()
    {
        // VAT-payer order, whole order, AdminDiscretion. Fee deducts FIRST off the gross, THEN VAT/net
        // derive from the seam-confirmed (post-fee) amount — not the pre-fee gross.
        // total 10 @21% VAT; fee 4.05% of 10 = 0.405 → 0.41 → confirmed 9.59.
        // VAT off 9.59 = round(9.59 × 21/121) = round(1.6643…) = 1.66; net = 9.59 − 1.66 = 7.93 (hand-derived).
        var order = SingleServiceOrder(totalPrice: 10m, appliedVatRate: 21m);
        Arrange(order);
        ArrangeCountryFee(rate: 4.05m, fixedFee: 0m);

        var result = await CreateHandler().Handle(AdminDiscretionWholeOrder(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(9.59m, result.Value!.RefundAmount);
        Assert.Equal(1.66m, result.Value.RefundVat);
        Assert.Equal(7.93m, _loyaltyService.LastRefundNet);
    }

    private sealed class RecordingRefundService : IRefundService
    {
        public RefundRequest? LastRequest { get; private set; }

        public Task<BusinessResult<RefundResult>> IssueRefundAsync(
            RefundRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(BusinessResult.Success(new RefundResult(
                RefundId: "refund-1",
                RefundKey: $"refund:{request.OrderId}:admin:{request.RefundRequestId}",
                Amount: request.Amount,
                Status: RefundStatus.Succeeded,
                ResolvedToExisting: false)));
        }
    }

    private sealed class RecordingLoyaltyService : ILoyaltyService
    {
        public decimal? LastRefundNet { get; private set; }

        public Task RevokeForPartialRefundAsync(
            string orderId, decimal refundNet, string refundKey, string actorId, CancellationToken cancellationToken)
        {
            LastRefundNet = refundNet;
            return Task.CompletedTask;
        }

        public Task GrantForCompletedOrderAsync(string orderId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RevokeForCancelledOrderAsync(string orderId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<TierDiscountResult> ResolveTierDiscountForOrderAsync(string userId, decimal orderTotal, CancellationToken cancellationToken)
            => Task.FromResult(new TierDiscountResult(0m, null));
        public Task GrantPointsManuallyAsync(string userId, int points, Cleansia.Core.Domain.Loyalty.LoyaltyEarnSource source, string? orderId, string actorId, string? reason, string? requestId, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public Task RevokePointsManuallyAsync(string userId, int points, Cleansia.Core.Domain.Loyalty.LoyaltyEarnSource source, string? orderId, string actorId, string? reason, string? requestId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
