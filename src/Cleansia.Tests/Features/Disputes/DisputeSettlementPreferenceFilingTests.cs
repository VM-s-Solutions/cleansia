using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// The customer states how a justified complaint is to be settled when they file it: a card refund
/// unless they choose credit. Filing without a choice is a card refund.
/// </summary>
public sealed class DisputeSettlementPreferenceFilingTests
{
    private const string CustomerId = "customer-preference-1";
    private const string OrderId = "order-preference-1";

    private readonly Mock<IDisputeRepository> _disputes = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public DisputeSettlementPreferenceFilingTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerId);
        _disputes
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        _orders.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
    }

    [Fact]
    public async Task A_Customer_Who_Chooses_Credit_Has_It_Recorded()
    {
        var filed = await FileAsync(new CreateDispute.Command(
            OrderId, DisputeReason.QualityIssue, "The bathroom was not cleaned at all.",
            SettlementPreference: DisputeSettlementPreference.Credit));

        Assert.Equal(DisputeSettlementPreference.Credit, filed.SettlementPreference);
    }

    [Fact]
    public async Task Filing_Without_A_Choice_Is_A_Card_Refund()
    {
        var filed = await FileAsync(new CreateDispute.Command(
            OrderId, DisputeReason.QualityIssue, "The bathroom was not cleaned at all."));

        Assert.Equal(DisputeSettlementPreference.CardRefund, filed.SettlementPreference);
    }

    [Fact]
    public async Task An_Undefined_Choice_Is_Refused()
    {
        var access = new Mock<IOrderAccessService>();
        access.Setup(a => a.OrderExistsForCallerAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new CreateDispute.Validator(access.Object).ValidateAsync(new CreateDispute.Command(
            OrderId, DisputeReason.QualityIssue, "The bathroom was not cleaned at all.",
            SettlementPreference: (DisputeSettlementPreference)0));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.InvalidEnumValue, error.ErrorMessage);
        Assert.Equal(nameof(CreateDispute.Command.SettlementPreference), error.PropertyName);
    }

    private async Task<Dispute> FileAsync(CreateDispute.Command command)
    {
        Dispute? added = null;
        _disputes.Setup(r => r.Add(It.IsAny<Dispute>())).Callback((Dispute d) => added = d);

        var handler = (CreateDispute.Handler)Activator.CreateInstance(
            typeof(CreateDispute.Handler),
            _disputes.Object,
            Cleansia.Tests.Common.OrderAccessDoubles.Over(_orders, _session),
            _session.Object, Mock.Of<ITenantProvider>(),
            new AuditContext(), Mock.Of<IAdminNotifier>(), Mock.Of<IUserNotificationRepository>())!;

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        return added!;
    }
}
