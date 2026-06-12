using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// ResolveDispute issues a real refund through the one seam (ADR-0006). It delegates the money call to
/// <see cref="IRefundService"/> with Reason=DisputeResolution and the DisputeId set — it does NOT gain a
/// raw IStripeClientFactory. The dispute is marked Resolved with its RefundAmount; a retried resolve
/// collapses on the per-dispute RefundKey so exactly one Stripe refund results; an already-terminal
/// dispute is never re-resolved (its recorded refund is never overwritten); and a successful refund
/// dispatches the refund-success notification via IPendingDispatch (ADR-0002 D1), never a direct queue send.
/// </summary>
public class ResolveDisputeRefundSeamTests
{
    private const string DisputeId = "dispute-1";
    private const string OrderId = "order-1";
    private const string ActorId = "admin-9";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();
    private readonly Mock<IPendingDispatch> _pending = new();

    public ResolveDisputeRefundSeamTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private ResolveDispute.Handler CreateHandler() =>
        new(_disputeRepository.Object, _session.Object, _refundService.Object, _pending.Object);

    private static Dispute NewPendingDispute() =>
        new(
            orderId: OrderId,
            userId: "customer-1",
            reason: DisputeReason.Other,
            description: "x",
            createdBy: "customer-1")
        {
            Id = DisputeId,
        };

    private Dispute ArrangeDispute()
    {
        var dispute = NewPendingDispute();
        _disputeRepository
            .Setup(r => r.GetDisputeWithDetailsAsync(DisputeId))
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
    public async Task Resolve_WithSuccessfulRefund_DispatchesRefundNotificationViaPendingDispatch()
    {
        ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", 250m, RefundStatus.Succeeded, false)));

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pending.Verify(p => p.Enqueue(
            QueueNames.NotificationsDispatch,
            It.Is<QueueEnvelope<SendPushNotificationMessage>>(e =>
                e.Payload.EventKey == NotificationEventCatalog.OrderRefunded
                && e.Payload.UserId == "customer-1"),
            It.IsAny<string>()), Times.Once);
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

        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(),
            It.IsAny<QueueEnvelope<SendPushNotificationMessage>>(),
            It.IsAny<string>()), Times.Never);
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
            .SetupSequence(r => r.GetDisputeWithDetailsAsync(DisputeId))
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
