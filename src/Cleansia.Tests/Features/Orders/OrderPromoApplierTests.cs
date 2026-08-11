using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Unit tests for <see cref="OrderPromoApplier"/> — the promo preview/apply collaborator extracted from
/// <c>CreateOrder.Handler</c>. Covers every guard the collaborator owns (no code / no user / failed
/// preview yield no discount; an order the promo did not price, or no code / no user, skip apply) and
/// the best-effort logged-and-swallowed apply semantics.
///
/// ADR-0038 §D5.1 moved the apply gate off the PREVIEW and onto the PERSISTED order, and swapped the
/// re-grossed <c>order.TotalPrice + preview.DiscountAmount</c> subtotal for the handler's own
/// <c>rawSubtotal</c>. The two tests that pinned the old shape were updated, not deleted.
/// </summary>
public class OrderPromoApplierTests
{
    private const string UserId = "user-1";
    private const string PromoCode = "SAVE10";
    private const string CurrencyId = "czk";
    private const string PromoCodeId = "promo-1";

    private readonly Mock<IPromoCodeService> _promoCodeService = new();

    private OrderPromoApplier CreateApplier() =>
        new(_promoCodeService.Object, NullLogger<OrderPromoApplier>.Instance);

    private static Order BuildOrder(
        decimal totalPrice = 900m,
        decimal? promoDiscountAmount = 100m,
        string? promoCodeId = PromoCodeId) =>
        OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = "order-1",
            UserId = UserId,
            TotalPrice = totalPrice,
            CustomerAddress = AddressMockFactory.Generate(),
            PromoDiscountAmount = promoDiscountAmount,
            PromoCodeId = promoCodeId,
        });

    [Fact]
    public async Task Preview_NoPromoCode_ReturnsNone_AndDoesNotCallService()
    {
        var command = CreateOrderTestData.ValidCommand(promoCode: null);

        var result = await CreateApplier().PreviewAsync(command, UserId, 900m, CurrencyId, CancellationToken.None);

        Assert.Same(OrderPromoPreview.None, result);
        _promoCodeService.Verify(
            s => s.PreviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Preview_NoUser_ReturnsNone_AndDoesNotCallService()
    {
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        var result = await CreateApplier().PreviewAsync(command, string.Empty, 900m, CurrencyId, CancellationToken.None);

        Assert.Same(OrderPromoPreview.None, result);
        _promoCodeService.Verify(
            s => s.PreviewAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Preview_ServiceFails_ReturnsNone()
    {
        _promoCodeService
            .Setup(s => s.PreviewAsync(PromoCode, UserId, 900m, CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodePreviewResult(false, 0m, null, PromoCodeError.Expired));
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        var result = await CreateApplier().PreviewAsync(command, UserId, 900m, CurrencyId, CancellationToken.None);

        Assert.Equal(0m, result.DiscountAmount);
        Assert.Null(result.PromoCodeId);
    }

    [Fact]
    public async Task Preview_ServiceSucceeds_AdoptsDiscountAndCodeId()
    {
        _promoCodeService
            .Setup(s => s.PreviewAsync(PromoCode, UserId, 1000m, CurrencyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodePreviewResult(true, 100m, PromoCodeId, null));
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        var result = await CreateApplier().PreviewAsync(command, UserId, 1000m, CurrencyId, CancellationToken.None);

        Assert.Equal(100m, result.DiscountAmount);
        Assert.Equal(PromoCodeId, result.PromoCodeId);
    }

    // ADR-0038 §D5.1 — OrderFactory.ResolveLoy003Discount discards the promo when membership+tier is
    // larger, and persists the order with PromoCodeId/PromoDiscountAmount null. Recording a redemption
    // there would burn the customer's one-shot code for a discount they never received. This test
    // replaces the old zero-PREVIEW gate assertion: the preview here is positive and irrelevant.
    [Fact]
    public async Task Apply_OrderPricedByMembershipNotPromo_DoesNotCallService()
    {
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);
        var order = BuildOrder(promoDiscountAmount: null, promoCodeId: null);

        await CreateApplier().ApplyAsync(
            command, UserId, order, 1000m, CurrencyId, CancellationToken.None);

        _promoCodeService.Verify(
            s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Apply_OrderCarriesCodeButZeroDiscount_DoesNotCallService()
    {
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);
        var order = BuildOrder(promoDiscountAmount: 0m, promoCodeId: PromoCodeId);

        await CreateApplier().ApplyAsync(
            command, UserId, order, 1000m, CurrencyId, CancellationToken.None);

        _promoCodeService.Verify(
            s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Apply_NoUser_DoesNotCallService()
    {
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        await CreateApplier().ApplyAsync(
            command, string.Empty, BuildOrder(), 1000m, CurrencyId, CancellationToken.None);

        _promoCodeService.Verify(
            s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Apply_OrderPricedByPromo_CallsServiceWithRawSubtotal()
    {
        _promoCodeService
            .Setup(s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeApplyResult(true, 100m, PromoCodeId, null));
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);
        var order = BuildOrder(totalPrice: 900m, promoDiscountAmount: 100m);

        await CreateApplier().ApplyAsync(
            command, UserId, order, 1000m, CurrencyId, CancellationToken.None);

        _promoCodeService.Verify(
            s => s.ApplyAsync(PromoCode, UserId, order.Id, 1000m, CurrencyId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ADR-0038 §D5.1 — on an EXPRESS order the surcharge is applied AFTER the discount
    // (OrderFactory: ApplyExpressSurcharge(rawSubtotal - applied)), so the old
    // `order.TotalPrice + preview.DiscountAmount` re-gross does not reconstruct the subtotal and the
    // recorded AppliedDiscount would be computed off an inflated base. 1000 raw − 100 promo = 900,
    // × (1 + 20% express) = 1080; the old expression would have passed 1180.
    [Fact]
    public async Task Apply_ExpressOrder_PassesRawSubtotal_NotTheReGrossedTotal()
    {
        _promoCodeService
            .Setup(s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeApplyResult(true, 100m, PromoCodeId, null));
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);
        const decimal rawSubtotal = 1000m;
        var expressTotal = BookingPolicy.ApplyExpressSurcharge(rawSubtotal - 100m, surchargeApplies: true);
        var order = BuildOrder(totalPrice: expressTotal, promoDiscountAmount: 100m);

        await CreateApplier().ApplyAsync(
            command, UserId, order, rawSubtotal, CurrencyId, CancellationToken.None);

        Assert.NotEqual(rawSubtotal, order.TotalPrice + 100m);
        _promoCodeService.Verify(
            s => s.ApplyAsync(PromoCode, UserId, order.Id, rawSubtotal, CurrencyId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Apply_ServiceFails_LogsAndSwallows()
    {
        _promoCodeService
            .Setup(s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeApplyResult(false, 0m, null, PromoCodeError.GlobalLimitReached));
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        var ex = await Record.ExceptionAsync(() => CreateApplier().ApplyAsync(
            command, UserId, BuildOrder(), 1000m, CurrencyId, CancellationToken.None));

        Assert.Null(ex);
    }
}
