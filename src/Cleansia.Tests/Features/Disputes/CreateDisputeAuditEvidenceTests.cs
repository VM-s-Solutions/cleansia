using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// ADR-0062 D3 at the producer: the marker names the ORDER so a refused filing's row resolves the order
/// it was refused against, and a successful filing re-labels its row to the DISPUTE it created with the
/// timing, the advertised window, the reason as an enum and the description as a length — never the
/// text. A refusal emits nothing; the pipeline writes it from the key and the command's <c>OrderId</c>.
/// </summary>
public sealed class CreateDisputeAuditEvidenceTests
{
    private const string CallerUserId = "caller-user-1";
    private const string OrderId = "order-dispute-1";
    private const string Description = "The kitchen floor was not mopped and the bins were left full.";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly AuditContext _auditContext = new();

    public CreateDisputeAuditEvidenceTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        _disputeRepository
            .Setup(r => r.GetOpenDisputeForOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);
    }

    private CreateDispute.Handler CreateHandler() =>
        new(_disputeRepository.Object, Cleansia.Tests.Common.OrderAccessDoubles.Over(_orderRepository, _session), _session.Object, Mock.Of<ITenantProvider>(), _auditContext);

    private Order ArrangeOrder(string? ownerUserId = CallerUserId, DateTime? cleaningDateTime = null, DateTime? completedAt = null)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: "customer@example.com",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime ?? DateTime.UtcNow.AddHours(-30),
            paymentType: PaymentType.Card,
            totalPrice: 1250m,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: ownerUserId);
        order.Id = OrderId;
        order.SetCurrency(currency);
        var service = Service.Create("cat-1", "Kitchen", "");
        service.Id = "svc-1";
        order.AddSelectedServices([OrderLineMockFactory.ServiceLine(order, service)]);
        if (completedAt is { } done)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
            typeof(Order).GetProperty(nameof(Order.CompletedAt))!.GetSetMethod(nonPublic: true)!.Invoke(order, [done]);
        }

        _orderRepository
            .Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        return order;
    }

    private static JsonElement Payload(AuditSnapshot? snapshot) => JsonDocument.Parse(snapshot!.AfterJson!).RootElement;

    [Fact]
    public void The_Marker_Names_The_Order_So_A_Refusal_Resolves_The_Order_Filed_Against()
    {
        var descriptor = AuditActionDescriptor.For(typeof(CreateDispute.Command));

        Assert.Equal("customer.dispute.create", descriptor.Action);
        Assert.Equal("Order", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.Equal(OrderId, AuditResourceResolver.ResolveExact(
            new CreateDispute.Command(OrderId, DisputeReason.QualityIssue, Description), descriptor.ResourceType));
    }

    [Fact]
    public async Task A_Filing_Inside_The_Window_Relabels_The_Row_To_The_Dispute_And_Records_Timing_Reason_And_Length()
    {
        ArrangeOrder(cleaningDateTime: DateTime.UtcNow.AddHours(-30), completedAt: DateTime.UtcNow.AddHours(-6));
        Dispute? added = null;
        _disputeRepository.Setup(r => r.Add(It.IsAny<Dispute>())).Callback<Dispute>(d => added = d);

        var result = await CreateHandler().Handle(
            new CreateDispute.Command(OrderId, DisputeReason.QualityIssue, Description,
                Lines: [new CreateDispute.DisputeLineSelection("svc-1", null)]),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Dispute", snapshot!.ResourceType);
        Assert.Equal(added!.Id, snapshot.ResourceId);

        var payload = Payload(snapshot);
        Assert.Equal(added.Id, payload.GetProperty("disputeId").GetString());
        Assert.Equal(OrderId, payload.GetProperty("orderId").GetString());
        Assert.Equal("qualityIssue", payload.GetProperty("reason").GetString());
        Assert.InRange(payload.GetProperty("hoursSinceCompletion").GetDecimal(), 5.9m, 6.1m);
        Assert.Equal(DisputeLimits.FilingWindowHours, payload.GetProperty("filingWindowHours").GetInt32());
        Assert.Equal(Description.Length, payload.GetProperty("descriptionLength").GetInt32());
        Assert.Equal(1, payload.GetProperty("lineCount").GetInt32());
        Assert.Equal(1250m, payload.GetProperty("orderTotalPrice").GetDecimal());
        Assert.Equal("CZK", payload.GetProperty("currencyCode").GetString());
        Assert.DoesNotContain(payload.EnumerateObject(), p => p.Name == "description");
        Assert.DoesNotContain("kitchen", snapshot.AfterJson!);
    }

    [Fact]
    public async Task A_Filing_Outside_The_Window_Is_Still_Accepted_And_The_Row_Says_How_Late_It_Was()
    {
        ArrangeOrder(cleaningDateTime: DateTime.UtcNow.AddHours(-80));

        var result = await CreateHandler().Handle(
            new CreateDispute.Command(OrderId, DisputeReason.ServiceNotProvided, Description), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Payload(_auditContext.DrainSnapshot());
        Assert.InRange(payload.GetProperty("hoursSinceCompletion").GetDecimal(), 79.9m, 80.1m);
        Assert.Equal(0, payload.GetProperty("lineCount").GetInt32());
    }

    [Fact]
    public async Task A_Filing_Before_The_Clean_Is_Refused_With_The_Key_And_Records_No_Evidence()
    {
        ArrangeOrder(cleaningDateTime: DateTime.UtcNow.AddDays(1));

        var result = await CreateHandler().Handle(
            new CreateDispute.Command(OrderId, DisputeReason.QualityIssue, Description), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeCleaningNotStarted, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    [Fact]
    public async Task A_Filing_Against_Another_Customers_Order_Is_Not_Found_And_Records_No_Evidence()
    {
        ArrangeOrder(ownerUserId: "someone-else");

        var result = await CreateHandler().Handle(
            new CreateDispute.Command(OrderId, DisputeReason.QualityIssue, Description), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, result.Error!.Message);
        Assert.Null(_auditContext.DrainSnapshot());
        _disputeRepository.Verify(r => r.Add(It.IsAny<Dispute>()), Times.Never);
    }
}
