using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ToS §19 on the guest surfaces: the guest preview quotes, and the guest cancel charges, the schedule
/// frozen on the order at booking — never today's <see cref="BookingPolicy"/>. Both run over one order
/// whose figures differ from today's on every count, so a reader of the constants cannot pass.
/// </summary>
public class GuestCancellationFrozenTermsTests
{
    private const string OrderId = "order-guest-frozen";

    /// <summary>Free 48 h, partial 30 % down to 6 h, last-minute 60 %.</summary>
    private static readonly CancellationTerms FrozenElsewhere = new(
        FreeHours: 48, PartialHours: 6, PartialFeeRate: 0.30m, LastMinuteFeeRate: 0.60m, PlusFreeHours: 8);

    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IGuestOrderAccessTokenRepository> _tokens = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<IExpressWaiverConsumer> _expressWaiverConsumer = ExpressWaiverMocks.NoConsumer();
    private string _token = null!;

    public GuestCancellationFrozenTermsTests()
    {
        _tokens
            .Setup(r => r.GetLiveForOrderIgnoringTenantAsync(OrderId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) =>
                BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{req.OrderId}:cancel", req.Amount, RefundStatus.Succeeded, false)));
    }

    private GuestOrderAccess Access => new(_orders.Object, _tokens.Object);

    private CancellationPolicyResolver Resolver => new(Mock.Of<IUserMembershipRepository>(), _orders.Object);

    private GetGuestCancellationFeePreview.Handler PreviewHandler() =>
        new(Access, Resolver, _expressWaiverConsumer.Object, TimeProvider.System);

    private CancelGuestOrder.Handler CancelHandler() =>
        new(
            Access,
            new GuestOrderAccessTokenIssuer(_tokens.Object),
            new CustomerOrderCancellation(
                Mock.Of<ITenantProvider>(),
                _refundService.Object,
                Mock.Of<IRefundRepository>(),
                Mock.Of<IReceivableRepository>(),
                Mock.Of<ICreditAccountRepository>(),
                Mock.Of<ILoyaltyService>(),
                Resolver,
                Mock.Of<INotificationProducer>(),
                Mock.Of<ILiveActivityProducer>(),
                _expressWaiverConsumer.Object,
                Mock.Of<IPendingDispatch>(),
                new AuditContext(),
                TimeProvider.System,
                NullLogger<CustomerOrderCancellation>.Instance),
            Mock.Of<IPendingDispatch>());

    private Order ArrangeGuestOrder(double cleaningInHours, CancellationTerms terms)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Guest",
            customerEmail: "guest@example.test",
            customerPhone: "+420111222333",
            customerAddress: Address.Create("Main 1", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(cleaningInHours),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: terms);
        order.Id = OrderId;
        order.Created("tester", DateTime.UtcNow.AddDays(-2));
        order.SetCurrency(currency);
        order.AssignStripeSessionId("cs_test_guest_frozen");
        var track = OrderStatusTrack.Create(OrderStatus.Confirmed, order);
        track.Created("tester", DateTimeOffset.UtcNow.AddDays(-2));
        order.AddOrderStatus(track);
        order.AddAssignedEmployee(OrderEmployee.Create(
            order, ValidatorTestHelpers.BuildEmployee("emp-guest-frozen", ContractStatus.Approved)));

        var token = GuestOrderAccessToken.Issue(OrderId, DateTimeOffset.UtcNow.AddDays(30));
        _token = token.RawToken!;
        _orders.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { order }.AsQueryable().BuildMock());
        _tokens.Setup(r => r.GetQueryableIgnoringTenant()).Returns(new[] { token }.AsQueryable().BuildMock());
        return order;
    }

    [Fact]
    public async Task Thirty_Hours_Out_The_Guest_Is_Quoted_And_Charged_The_Orders_Own_30Percent()
    {
        // 30h before start: free under today's 24h, but this order's free window is 48h and its partial
        // threshold 6h → partial at its own 0.30. Hand-derived: refund 1000 × 0.70 = 700, fee 300.
        var order = ArrangeGuestOrder(cleaningInHours: 30, FrozenElsewhere);

        var preview = await PreviewHandler().Handle(
            new GetGuestCancellationFeePreview.Query(_token), CancellationToken.None);
        var cancel = await CancelHandler().Handle(new CancelGuestOrder.Command(_token), CancellationToken.None);

        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.Equal(CancellationFeeTier.Partial, preview.Value!.Tier);
        Assert.Equal(0.30m, preview.Value.FeeRate);
        Assert.Equal(700m, preview.Value.RefundAmount);
        Assert.Equal(300m, preview.Value.FeeAmount);

        Assert.True(cancel.IsSuccess, cancel.Error?.Message);
        Assert.Equal(0.30m, cancel.Value!.FeeRate);
        Assert.Equal(700m, cancel.Value.RefundAmount);
        Assert.Equal(0.30m, order.CancellationFeeRate);
        Assert.Equal(700m, order.CancellationRefundAmount);
    }

    [Fact]
    public async Task Five_Hours_Out_The_Guest_Is_Quoted_And_Charged_The_Orders_Own_60Percent()
    {
        // 5h before start: today's 25% tier (≥ 4h), but under this order's 6h partial threshold it is
        // last-minute at its own 0.60. Hand-derived: refund 1000 × 0.40 = 400, fee 600.
        var order = ArrangeGuestOrder(cleaningInHours: 5, FrozenElsewhere);

        var preview = await PreviewHandler().Handle(
            new GetGuestCancellationFeePreview.Query(_token), CancellationToken.None);
        var cancel = await CancelHandler().Handle(new CancelGuestOrder.Command(_token), CancellationToken.None);

        Assert.True(preview.IsSuccess, preview.Error?.Message);
        Assert.Equal(CancellationFeeTier.LastMinute, preview.Value!.Tier);
        Assert.Equal(0.60m, preview.Value.FeeRate);
        Assert.Equal(400m, preview.Value.RefundAmount);

        Assert.True(cancel.IsSuccess, cancel.Error?.Message);
        Assert.Equal(0.60m, cancel.Value!.FeeRate);
        Assert.Equal(400m, order.CancellationRefundAmount);
    }
}
