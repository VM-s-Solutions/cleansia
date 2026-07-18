using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// ResolveDispute issues a real refund through the one seam (ADR-0006). It delegates the money call to
/// <see cref="IRefundService"/> with Reason=DisputeResolution and the DisputeId set — it does NOT gain a
/// raw IStripeClientFactory. The dispute is marked Resolved with its RefundAmount; a retried resolve
/// collapses on the per-dispute RefundKey so exactly one Stripe refund results; an already-terminal
/// dispute is never re-resolved (its recorded refund is never overwritten); and a successful refund
/// records the refund-success notification via the shared INotificationProducer seam, never a direct
/// queue send.
/// </summary>
public class ResolveDisputeRefundSeamTests
{
    private const string DisputeId = "dispute-1";
    private const string OrderId = "order-1";
    private const string ActorId = "admin-9";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<INotificationProducer> _producer = new();

    public ResolveDisputeRefundSeamTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private readonly AuditContext _auditContext = new();

    private ResolveDispute.Handler CreateHandler() =>
        new(_disputeRepository.Object, _session.Object, _refundService.Object, _producer.Object, _auditContext);

    private static Dispute NewPendingDispute()
    {
        var dispute = new Dispute(
            orderId: OrderId,
            userId: "customer-1",
            reason: DisputeReason.Other,
            description: "x",
            createdBy: "customer-1")
        {
            Id = DisputeId,
        };

        // GetForUpdateAsync materializes the Order reference nav; the mock mirrors that contract
        // (EF-only nav — attached by reflection, same as the Order.Receipt arranges).
        typeof(Dispute).GetProperty(nameof(Dispute.Order))!.SetValue(dispute, NewOrder());
        return dispute;
    }

    private static Order NewOrder()
    {
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Paid,
            userId: "customer-1");
        order.Id = OrderId;
        return order;
    }

    private Dispute ArrangeDispute()
    {
        var dispute = NewPendingDispute();
        _disputeRepository
            .Setup(r => r.GetForUpdateAsync(DisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        return dispute;
    }

    [Fact]
    public async Task Resolve_WithRefundAmount_IssuesRefundViaSeam_WithDisputeResolutionReason_AndDisputeId()
    {
        var dispute = ArrangeDispute();
        RefundRequest? captured = null;
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .Callback<RefundRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", 250m, RefundStatus.Succeeded, false)));

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(captured);
        Assert.Equal(OrderId, captured!.OrderId);
        Assert.Equal(250m, captured.Amount);
        Assert.Equal(RefundReason.DisputeResolution, captured.Reason);
        Assert.Equal(DisputeId, captured.DisputeId);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
        Assert.Equal(250m, dispute.RefundAmount);
    }

    [Fact]
    public async Task Resolve_WithSuccessfulRefund_RecordsRefundNotificationViaTheSeam()
    {
        ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", 250m, RefundStatus.Succeeded, false)));

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _producer.Verify(p => p.NotifyAsync(
            "customer-1",
            NotificationEventCatalog.OrderRefunded,
            It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(),
            OrderId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resolve_RefundNotification_Args_CarryTheOrdersDisplayNumber()
    {
        var dispute = ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", 250m, RefundStatus.Succeeded, false)));
        Dictionary<string, string>? capturedArgs = null;
        _producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, _, args, _, _, _) => capturedArgs = args)
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedArgs);
        Assert.False(string.IsNullOrEmpty(dispute.Order.DisplayOrderNumber));
        Assert.Equal(dispute.Order.DisplayOrderNumber, capturedArgs!["orderNumber"]);
        Assert.Equal(OrderId, capturedArgs["orderId"]);
        Assert.Equal(DisputeId, capturedArgs["disputeId"]);
    }

    [Fact]
    public async Task Resolve_WhenRefundFails_DoesNotDispatchNotification()
    {
        ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));

        await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_WithoutRefundAmount_RecordsResolution_DoesNotCallSeam()
    {
        var dispute = ArrangeDispute();

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, null, "no refund warranted"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_WithZeroRefundAmount_RecordsResolution_DoesNotCallSeam()
    {
        var dispute = ArrangeDispute();

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 0m, "no refund warranted"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, dispute.RefundAmount);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_OnAlreadyResolvedDispute_IsRejected_DoesNotCallSeam_AndKeepsOriginalRefund()
    {
        var dispute = ArrangeDispute();
        dispute.Resolve(resolvedBy: ActorId, refundAmount: 100m, resolutionNotes: "first resolution");

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 999m, "second resolution overwriting the refund"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeAlreadyResolved, result.Error!.Message);
        Assert.Equal(100m, dispute.RefundAmount);
        Assert.Equal("first resolution", dispute.ResolutionNotes);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_OnClosedDispute_IsRejected_DoesNotCallSeam()
    {
        var dispute = ArrangeDispute();
        dispute.UpdateStatus(DisputeStatus.Closed, ActorId);

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 50m, "resolving a closed dispute"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeAlreadyResolved, result.Error!.Message);
        _refundService.Verify(
            s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_Retried_UsesPerDisputeKey_ResolvesToExisting_NoSecondStripeRefund()
    {
        // A redelivery (e.g. the first commit was rolled back, or a duplicate request) re-runs the handler
        // against a still-Pending dispute. Each call drives the seam, which collapses on the deterministic
        // per-dispute RefundKey (refund:{OrderId}:dispute:{DisputeId}) so exactly one Stripe refund results
        // — the SECOND call comes back ResolvedToExisting=true. The terminal guard is what blocks a re-resolve
        // of an ALREADY-Resolved dispute (covered above); the seam key is what makes a Pending redelivery safe.
        _disputeRepository
            .SetupSequence(r => r.GetForUpdateAsync(DisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(NewPendingDispute())
            .ReturnsAsync(NewPendingDispute());

        var calls = 0;
        _refundService
            .Setup(s => s.IssueRefundAsync(
                It.Is<RefundRequest>(r => r.Reason == RefundReason.DisputeResolution && r.DisputeId == DisputeId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                calls++;
                return BusinessResult.Success(new RefundResult(
                    "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", 250m, RefundStatus.Succeeded,
                    ResolvedToExisting: calls > 1));
            });

        var first = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);
        var second = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        _refundService.Verify(
            s => s.IssueRefundAsync(
                It.Is<RefundRequest>(r => r.DisputeId == DisputeId), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
