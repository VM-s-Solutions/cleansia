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
/// preview yield no discount; zero-discount / no-code / no-user skip apply) and the best-effort
/// logged-and-swallowed apply semantics, so the extraction carries the same behavior the handler
/// characterization suite pins.
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

    private static Order BuildOrder(decimal totalPrice = 900m) =>
        OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            Id = "order-1",
            UserId = UserId,
            TotalPrice = totalPrice,
            CustomerAddress = AddressMockFactory.Generate(),
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

    [Fact]
    public async Task Apply_ZeroDiscount_DoesNotCallService()
    {
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);

        await CreateApplier().ApplyAsync(
            command, UserId, BuildOrder(), OrderPromoPreview.None, CurrencyId, CancellationToken.None);

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
            command, string.Empty, BuildOrder(), new OrderPromoPreview(100m, PromoCodeId), CurrencyId, CancellationToken.None);

        _promoCodeService.Verify(
            s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Apply_PositiveDiscount_CallsServiceWithReGrossedSubtotal()
    {
        _promoCodeService
            .Setup(s => s.ApplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromoCodeApplyResult(true, 100m, PromoCodeId, null));
        var command = CreateOrderTestData.ValidCommand(promoCode: PromoCode);
        var order = BuildOrder(totalPrice: 900m);

        await CreateApplier().ApplyAsync(
            command, UserId, order, new OrderPromoPreview(100m, PromoCodeId), CurrencyId, CancellationToken.None);

        _promoCodeService.Verify(
            s => s.ApplyAsync(PromoCode, UserId, order.Id, order.TotalPrice + 100m, CurrencyId, It.IsAny<CancellationToken>()),
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
            command, UserId, BuildOrder(), new OrderPromoPreview(100m, PromoCodeId), CurrencyId, CancellationToken.None));

        Assert.Null(ex);
    }
}
