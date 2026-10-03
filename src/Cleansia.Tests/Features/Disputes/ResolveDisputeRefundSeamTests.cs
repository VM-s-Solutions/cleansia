using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Orders;
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
    private readonly Mock<ICreditAccountRepository> _creditAccountRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _payRepository = new();
    private readonly Mock<ILoyaltyService> _loyalty = new();

    public ResolveDisputeRefundSeamTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private readonly AuditContext _auditContext = new();

    private ResolveDispute.Handler CreateHandler() =>
        new(_disputeRepository.Object, _session.Object, _refundService.Object, Mock.Of<IRefundRepository>(),
            _creditAccountRepository.Object, _payRepository.Object, _loyalty.Object, _producer.Object, _auditContext);

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
            cleaningDateTime: DateTime.UtcNow.AddDays(5),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Paid,
            userId: "customer-1",
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
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

    // The request asks for 800 of a 1000 sale settled with 200 credit, after 500 already went back to
    // the card: the card has 300 left, so 300 moves to it, and the credit leg returns 160.
    [Fact]
    public async Task Resolve_WithARefundClampedByTheCardCeiling_RecordsTheRequestAndWhatMoved()
    {
        var dispute = ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", 300m, RefundStatus.Succeeded, false,
                CreditReturned: 160m)));

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 800m, "approved"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(800m, dispute.RefundAmount);
        Assert.Equal(300m, dispute.CardRefundedAmount);
        Assert.Equal(160m, dispute.CreditReturnedAmount);

        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Contains("\"refundAmount\":800", snapshot!.AfterJson);
        Assert.Contains("\"cardRefundedAmount\":300", snapshot.AfterJson);
        Assert.Contains("\"creditReturnedAmount\":160", snapshot.AfterJson);
        Assert.Contains("\"cardRefundedAmount\":null", snapshot.BeforeJson);
    }

    /// <summary>
    /// The dispute's settlement takes back its share of the order's points (customer terms §11), on what
    /// it actually returned: 300 to the card and 160 to the credit balance, 460 — not the 800 asked for.
    /// </summary>
    [Fact]
    public async Task Resolve_WithARefund_TakesBackTheShareOfPointsItReturned_CardPlusCredit()
    {
        ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", 300m, RefundStatus.Succeeded, false,
                CreditReturned: 160m)));

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 800m, "approved"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _loyalty.Verify(l => l.RevokeForRefundAsync(
            OrderId, 460m, $"dispute-settlement:{DisputeId}", ActorId, It.IsAny<CancellationToken>()), Times.Once);
        _loyalty.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public async Task Resolve_WithoutARefund_TakesNoPoints(double? refundAmount)
    {
        ArrangeDispute();

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, (decimal?)refundAmount, "no refund warranted"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _loyalty.VerifyNoOtherCalls();
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
                It.Is<string>(subject => !string.IsNullOrWhiteSpace(subject)
                    && subject != OrderId),
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

    /// <summary>
    /// The upheld complaint whose money never moved. The refund is attempted FIRST and the resolution
    /// is written only once Stripe has confirmed, so a refused refund leaves the dispute exactly where
    /// it was — still open, no RefundAmount, no resolution notes, nobody told the customer they were
    /// paid — and the command answers the seam's own error rather than success.
    /// </summary>
    [Fact]
    public async Task Resolve_WhenRefundFails_LeavesTheDisputeUnresolved_AndReturnsTheSeamsError()
    {
        var dispute = ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundFailed)));

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundFailed, result.Error!.Message);
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
        Assert.Null(dispute.RefundAmount);
        Assert.Null(dispute.ResolutionNotes);
        Assert.Null(dispute.ResolvedOn);
        Assert.Null(_auditContext.DrainSnapshot());

        _producer.Verify(p => p.NotifyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _loyalty.VerifyNoOtherCalls();
    }

    /// <summary>
    /// A refund the seam refuses for a reason of its own — nothing left to refund — propagates that
    /// reason, not a flattened <c>refund.failed</c>: the administrator needs to know the order has
    /// already been refunded rather than that Stripe was unreachable.
    /// </summary>
    [Fact]
    public async Task Resolve_WhenNothingIsRefundable_PropagatesThatReason()
    {
        var dispute = ArrangeDispute();
        _refundService
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure<RefundResult>(
                new Error(nameof(RefundRequest.Amount), BusinessErrorMessage.RefundNothingRefundable)));

        var result = await CreateHandler().Handle(
            new ResolveDispute.Command(DisputeId, 250m, "approved"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
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
        // Both attempts hand the clawback the one per-dispute key, on which the second collapses.
        _loyalty.Verify(l => l.RevokeForRefundAsync(
            OrderId, 250m, $"dispute-settlement:{DisputeId}", ActorId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _loyalty.VerifyNoOtherCalls();
    }
}
