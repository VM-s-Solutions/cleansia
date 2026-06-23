using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// The admin partial-refund command handler (ADR-0009). Drives the policy (window + override + fee),
/// the share-of-<c>TotalPrice</c> allocator, the <c>IRefundService</c> seam (deterministic admin
/// RefundKey), the loyalty clawback, and the PaymentStatus summary. The seam itself is unit-tested in
/// <c>RefundServiceTests</c>; here it is a recording fake so we assert the policy-correct Amount the
/// handler hands it.
/// </summary>
public class IssuePartialRefundHandlerTests
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

    public IssuePartialRefundHandlerTests()
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

    private static Order CreateOrder(
        decimal totalPrice,
        decimal? appliedVatRate,
        bool completed,
        IEnumerable<Service>? services = null,
        IEnumerable<Package>? packages = null,
        string? countryId = CountryId,
        int rooms = 2,
        int bathrooms = 1)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var address = countryId is null
            ? null!
            : Address.Create("Street 1", "Prague", "11000", countryId);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: rooms,
            bathrooms: bathrooms,
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
        if (services is not null)
        {
            order.AddSelectedServices(services.Select(s => OrderService.Create(order, s)));
        }
        if (packages is not null)
        {
            order.AddSelectedPackages(packages.Select(p => OrderPackage.Create(order, p)));
        }
        if (completed)
        {
            order.CompleteOrder(actualCompletionTime: 120);
        }
        return order;
    }

    private void ArrangeOrder(Order order) =>
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

    private void ArrangeConsumed(decimal consumed) =>
        _refundRepository
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consumed);

    private static Service Svc(string id, decimal basePrice, decimal perRoomPrice = 0m)
    {
        var s = Service.Create("cat-1", $"Service {id}", "", basePrice, perRoomPrice);
        s.Id = id;
        return s;
    }

    // TC-REFUND-WINDOW — within window, no override needed.
    [Fact]
    public async Task WithinWindow_AllowsRefund_WithoutOverrideReason()
    {
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: true, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.WindowOverridden);
        Assert.Equal(1, _refundService.CallCount);
    }

    // TC-REFUND-WINDOW — closed window (null CompletedAt) requires a non-empty override reason.
    [Fact]
    public async Task ClosedWindow_WithoutOverrideReason_IsRejected()
    {
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: false, services: [svc]);
        ArrangeOrder(order);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: "   "),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundOverrideReasonRequired, result.Error!.Message);
        Assert.Equal(0, _refundService.CallCount);
    }

    // TC-REFUND-WINDOW — closed window WITH a persisted override reason proceeds and flags the override.
    [Fact]
    public async Task ClosedWindow_WithOverrideReason_ProceedsAndFlagsOverride()
    {
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: false, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: "Goodwill — late complaint, verified."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WindowOverridden);
        Assert.Equal(1, _refundService.CallCount);
    }

    // TC-REFUND-FEE — platform absorbs the Stripe fee on ServiceNotRendered: full line amount is sent.
    [Fact]
    public async Task ServiceNotRendered_SendsFullLineAmount_PlatformAbsorbsFee()
    {
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: true, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);

        await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);

        Assert.Equal(1000m, _refundService.LastRequest!.Amount);
    }

    // TC-REFUND-FEE — AdminDiscretion deducts the per-country Stripe fee from the refunded amount.
    [Fact]
    public async Task AdminDiscretion_DeductsTheStripeFee_FromTheRefundedAmount()
    {
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: true, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(986m);
        ArrangeCountryFee(rate: 1.4m, fixedFee: 6m);

        await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.AdminDiscretion,
                OverrideReason: null),
            CancellationToken.None);

        // CZ config 1.4% of 1000 + 6 = 20 fee → 980 sent.
        Assert.Equal(980m, _refundService.LastRequest!.Amount);
    }

    // FINDING 4 — AdminDiscretion with NO per-country config: fee = 0 (platform absorbs, fail-open for
    // the customer), the full allocated amount is sent, no throw.
    [Fact]
    public async Task AdminDiscretion_NullCountryConfig_ChargesNoFee_SendsFullAmount()
    {
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: true, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);
        // No ArrangeCountryFee → GetByCountryIdAsync returns null.

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.AdminDiscretion,
                OverrideReason: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000m, _refundService.LastRequest!.Amount);
        Assert.Equal(1000m, result.Value!.RefundAmount);
    }

    // FINDING 2 — fee/VAT/net all derive from the seam-confirmed amount, never the pre-fee gross.
    // Architect oracle: TotalPrice 1210, VAT 21%, whole order, AdminDiscretion, CZ fee 1.4%+6.
    [Fact]
    public async Task AdminDiscretion_VatPayer_VatAndNetDeriveFromConfirmedAmount_NotPreFee()
    {
        var svc = Svc("svc-a", 1210m);
        var order = CreateOrder(1210m, appliedVatRate: 21m, completed: true, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1210m);
        ArrangeCountryFee(rate: 1.4m, fixedFee: 6m);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.AdminDiscretion,
                OverrideReason: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        // fee = round(1210 * 0.014 + 6) = 22.94 → sent/confirmed = 1187.06.
        Assert.Equal(1187.06m, _refundService.LastRequest!.Amount);
        Assert.Equal(1187.06m, result.Value!.RefundAmount);
        // VAT and net off the CONFIRMED amount (1187.06), not the pre-fee 1210 (the split-brain bug, which
        // would report VAT 210.00 / net 977.06). round(1187.06 * 21/121) = 206.02; net = 1187.06 - 206.02.
        Assert.Equal(206.02m, result.Value!.RefundVat);
        Assert.Equal(981.04m, _loyaltyService.LastRefundNet);
    }

    // FINDING 1 — a standalone service's gross ratio weight MUST include PerRoomPrice × (rooms+bathrooms)
    // (ADR-0009 D5.1). With PerRoomPrice > 0 and rooms+bathrooms > 0 the split must differ from the
    // PerRoomPrice = 0 baseline.
    [Fact]
    public async Task StandaloneServiceGross_IncludesPerRoomComponent_ShiftsTheRatioSplit()
    {
        // rooms + bathrooms = 4. Two standalone services; both BasePrice 400.
        // Baseline (PerRoomPrice 0): grosses 400/400 → svc-a share of TotalPrice 800 = round(400/800*800)=400.
        var aBaseline = Svc("svc-a", 400m, perRoomPrice: 0m);
        var bBaseline = Svc("svc-b", 400m, perRoomPrice: 0m);
        var baselineOrder = CreateOrder(800m, appliedVatRate: null, completed: true,
            services: [aBaseline, bBaseline], rooms: 2, bathrooms: 2);
        ArrangeOrder(baselineOrder);
        ArrangeConsumed(800m);

        var baseline = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);

        // With PerRoomPrice: svc-a gross = 400 + 50*4 = 600; svc-b gross = 400.
        // svc-a share of TotalPrice 800 = round(600/1000*800) = 480 ≠ 400 baseline.
        var aPerRoom = Svc("svc-a", 400m, perRoomPrice: 50m);
        var bPerRoom = Svc("svc-b", 400m, perRoomPrice: 0m);
        var perRoomOrder = CreateOrder(800m, appliedVatRate: null, completed: true,
            services: [aPerRoom, bPerRoom], rooms: 2, bathrooms: 2);
        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(perRoomOrder);
        _refundService.Reset();

        var perRoom = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);

        Assert.True(baseline.IsSuccess && perRoom.IsSuccess);
        Assert.Equal(400m, baseline.Value!.RefundAmount);
        Assert.Equal(480m, perRoom.Value!.RefundAmount);
        Assert.NotEqual(baseline.Value!.RefundAmount, perRoom.Value!.RefundAmount);
    }

    // FINDING 3 — a closed-window refund with an override reason persists that reason on the Refund row
    // (the audit trail, ADR-0009 D1), threaded command → RefundRequest → Refund.Create.
    [Fact]
    public async Task ClosedWindow_PersistsOverrideReason_OnTheRefundRequest()
    {
        const string reason = "Goodwill — late complaint, verified.";
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: false, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: reason),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WindowOverridden);
        Assert.Equal(reason, _refundService.LastRequest!.WindowOverrideReason);
    }

    // FINDING 3 — an in-window refund never carries a stray override reason onto the seam.
    [Fact]
    public async Task WithinWindow_DoesNotCarryOverrideReason()
    {
        var svc = Svc("svc-a", 1000m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: true, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);

        await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: "ignored in-window"),
            CancellationToken.None);

        Assert.Null(_refundService.LastRequest!.WindowOverrideReason);
    }

    // TC-REFUND-CEILING — partial of charge → PartiallyRefunded; order lifecycle stays Completed.
    [Fact]
    public async Task PartialOfCharge_SummarisesAsPartiallyRefunded()
    {
        var a = Svc("svc-a", 500m);
        var b = Svc("svc-b", 500m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: true, services: [a, b]);
        ArrangeOrder(order);
        ArrangeConsumed(500m);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.PartiallyRefunded, result.Value!.PaymentStatus);
        Assert.NotNull(order.CompletedAt); // lifecycle stays Completed
    }

    // TC-REFUND-CEILING — at equality (whole charge refunded) → Refunded.
    [Fact]
    public async Task WholeChargeRefunded_SummarisesAsRefunded()
    {
        var a = Svc("svc-a", 500m);
        var b = Svc("svc-b", 500m);
        var order = CreateOrder(1000m, appliedVatRate: null, completed: true, services: [a, b]);
        ArrangeOrder(order);
        ArrangeConsumed(1000m);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [
                    new IssuePartialRefund.RefundLineSelection("svc-a", null),
                    new IssuePartialRefund.RefundLineSelection("svc-b", null),
                ],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Refunded, result.Value!.PaymentStatus);
    }

    // TC-REFUND-VAT — apportioned VAT is reported and the loyalty clawback is on net (VAT-excluded).
    [Fact]
    public async Task VatIsApportioned_AndLoyaltyClawbackIsOnNet()
    {
        var svc = Svc("svc-a", 1210m);
        var order = CreateOrder(1210m, appliedVatRate: 21m, completed: true, services: [svc]);
        ArrangeOrder(order);
        ArrangeConsumed(1210m);

        var result = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(210m, result.Value!.RefundVat);                 // round(1210 * 21/121)
        Assert.Equal(1000m, _loyaltyService.LastRefundNet);          // net = 1210 - 210
    }

    // TC-REFUND-BUNDLED — refunding included services within a package never exceeds the package line's
    // share of TotalPrice (uses PackagePricing.DeriveIncludedServiceGrosses under the hood).
    [Fact]
    public async Task BundledServiceRefunds_NeverExceedThePackageLinesShareOfTotalPrice()
    {
        var inc1 = Svc("inc-1", 0m);
        var inc2 = Svc("inc-2", 0m);
        var package = Package.Create("Deep clean bundle", "", 600m);
        package.Id = "pkg-1";
        package.AddService(inc1);
        package.AddService(inc2);

        // A standalone service line so the package is only PART of the order; TotalPrice = 800 (discounted
        // from a 1000 list: svc 400 + package 600). The package's share of 800 is round(600/1000*800)=480.
        var standalone = Svc("svc-a", 400m);
        var order = CreateOrder(800m, appliedVatRate: null, completed: true,
            services: [standalone], packages: [package]);
        ArrangeOrder(order);
        ArrangeConsumed(800m);

        // Refund the first included service.
        var first = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("inc-1", "pkg-1")],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);
        var firstAmount = first.Value!.RefundAmount;

        // Then refund the rest of the bundle.
        _refundService.Reset();
        var second = await CreateHandler().Handle(
            new IssuePartialRefund.Command(
                OrderId,
                [new IssuePartialRefund.RefundLineSelection("inc-2", "pkg-1")],
                RefundReason.ServiceNotRendered,
                OverrideReason: null),
            CancellationToken.None);
        var secondAmount = second.Value!.RefundAmount;

        Assert.True(first.IsSuccess && second.IsSuccess);
        // The package line's share of TotalPrice 800 is round(600/1000*800) = 480.
        Assert.Equal(480m, firstAmount + secondAmount);
    }

    private sealed class RecordingRefundService : IRefundService
    {
        public int CallCount { get; private set; }
        public RefundRequest? LastRequest { get; private set; }

        public void Reset()
        {
            CallCount = 0;
            LastRequest = null;
        }

        public Task<BusinessResult<RefundResult>> IssueRefundAsync(
            RefundRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
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
