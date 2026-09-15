using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0062 D3 at the producer: confirming a recurring occurrence emits ONE
/// <see cref="ConfirmRecurringOrder.RecurringOccurrenceConfirmationEvidence"/> with the occurrence's
/// price and schedule as the materializer stored them. A refusal — including the not-found that hides
/// another customer's order — emits nothing.
/// </summary>
public sealed class ConfirmRecurringOrderAuditEvidenceTests
{
    private const string OrderId = "order-recurring-ev-1";
    private const string CustomerUserId = "user-customer-1";
    private const string TemplateId = "tmpl-ev-1";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly AuditContext _auditContext = new();

    public ConfirmRecurringOrderAuditEvidenceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CustomerUserId);
    }

    private ConfirmRecurringOrder.Handler CreateHandler() =>
        new(
            _orderRepository.Object,
            new Mock<ICreditAccountRepository>().Object,
            new Mock<IUserRepository>().Object,
            _session.Object,
            new Mock<IStripeClient>().Object,
            new StripeConfig(new ConfigurationBuilder().Build()),
            new Mock<IPendingDispatch>().Object,
            new Mock<INotificationProducer>().Object,
            NoPreferredCleanerHold.Resolver,
            _auditContext,
            NullLogger<ConfirmRecurringOrder.Handler>.Instance);

    private Order ArrangeOrder(
        PaymentType paymentType = PaymentType.Cash,
        string? ownerUserId = CustomerUserId,
        PaymentStatus paymentStatus = PaymentStatus.Pending)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: Core.Domain.Users.Address.Create("123 Main St", "Prague", "11000", "cz"),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddHours(48),
            paymentType: paymentType,
            totalPrice: 900m,
            currencyId: currency.Id,
            paymentStatus: paymentStatus,
            userId: ownerUserId,
            recurringTemplateId: TemplateId);
        order.Id = OrderId;
        order.TenantId = "tenant-1";
        order.SetCurrency(currency);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        _orderRepository.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    [Fact]
    public void The_Marker_Is_Frozen_On_The_Order()
    {
        var descriptor = AuditActionDescriptor.For(typeof(ConfirmRecurringOrder.Command));

        Assert.Equal("customer.order.recurring.confirm", descriptor.Action);
        Assert.Equal("Order", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
    }

    [Fact]
    public async Task A_Cash_Confirmation_Records_The_Occurrence_Its_Template_Price_And_Lead_Time()
    {
        ArrangeOrder();

        var result = await CreateHandler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Order", snapshot!.ResourceType);
        Assert.Equal(OrderId, snapshot.ResourceId);

        var payload = JsonDocument.Parse(snapshot.AfterJson!).RootElement;
        Assert.Equal(OrderId, payload.GetProperty("orderId").GetString());
        Assert.Equal(TemplateId, payload.GetProperty("recurringTemplateId").GetString());
        Assert.Equal(900m, payload.GetProperty("totalPrice").GetDecimal());
        Assert.Equal("CZK", payload.GetProperty("currencyCode").GetString());
        Assert.Equal("cash", payload.GetProperty("paymentType").GetString());
        Assert.InRange(payload.GetProperty("leadTimeHours").GetDecimal(), 47.9m, 48.0m);
        Assert.Equal(7, payload.EnumerateObject().Count());
    }

    [Fact]
    public async Task An_Already_Paid_Occurrence_Is_Refused_And_Records_No_Evidence()
    {
        ArrangeOrder(paymentStatus: PaymentStatus.Paid);

        var result = await CreateHandler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderPaymentAlreadyPaid, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    [Fact]
    public async Task Another_Customers_Occurrence_Is_Not_Found_And_Records_No_Evidence()
    {
        ArrangeOrder(ownerUserId: "someone-else");

        var result = await CreateHandler().Handle(new ConfirmRecurringOrder.Command(OrderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }
}
