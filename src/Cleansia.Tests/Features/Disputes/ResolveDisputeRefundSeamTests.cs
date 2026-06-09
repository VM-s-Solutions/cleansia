using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// ResolveDispute issues a real refund through the one seam (ADR-0006): today it records
/// RefundAmount + Resolved but never calls Stripe. It now delegates the money call to
/// <see cref="IRefundService"/> with Reason=DisputeResolution and the DisputeId set — it does NOT gain a
/// raw IStripeClientFactory. The dispute is still marked Resolved with its RefundAmount, and a retried
/// resolve collapses on the per-dispute RefundKey so exactly one Stripe refund results.
/// </summary>
public class ResolveDisputeRefundSeamTests
{
    private const string DisputeId = "dispute-1";
    private const string OrderId = "order-1";
    private const string ActorId = "admin-9";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refundService = new();

    public ResolveDisputeRefundSeamTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private ResolveDispute.Handler CreateHandler() =>
        new(_disputeRepository.Object, _session.Object, _refundService.Object);

    private Dispute ArrangeDispute()
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
    public async Task Resolve_Retried_UsesPerDisputeKey_ResolvesToExisting_NoSecondStripeRefund()
    {
        ArrangeDispute();
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
