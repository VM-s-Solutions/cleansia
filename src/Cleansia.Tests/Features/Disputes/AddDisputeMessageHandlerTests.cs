using System.Security.Claims;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// T-0102 (SEC-DSP-01) / ADR-0001 §D2 Note C, verification #5/#6 — the inner gate of
/// <see cref="AddDisputeMessage.Handler"/>.
///
/// The handler is dual-purpose (customer self-reply vs. staff reply). This pins the two security
/// obligations of the ticket at the layer that holds on every invocation path:
///   - AC1 [OWN-DATA]: a customer may only message their OWN dispute (ownership check);
///   - AC3: the staff flag is DERIVED from the caller's profile, never blindly trusted from the
///     command body — a non-admin caller can never produce <c>isStaff=true</c> (so flipping the body
///     flag is inert) and the staff→customer push only fires for a genuine admin reply.
/// Written red → green per knowledge/testing.md (predates the handler hardening).
/// </summary>
public class AddDisputeMessageHandlerTests
{
    private const string CustomerSub = "customer-sub-1";
    private const string OtherCustomerSub = "customer-sub-2";
    private const string AdminSub = "admin-sub-9";
    private const string DisputeId = "dispute-1";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    // ADR-0002 D1/D5 — the handler now records intent via IPendingDispatch (post-commit dispatch)
    // instead of calling IQueueClient.SendAsync directly inside the handler (the F2/SEC-W1 fix).
    private readonly Mock<IPendingDispatch> _pending = new();

    private AddDisputeMessage.Handler CreateHandler() =>
        new(_disputeRepository.Object, _session.Object, _pending.Object);

    private void SetCaller(string sub, UserProfile role)
    {
        _session.Setup(s => s.GetUserId()).Returns(sub);
        _session.Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, role.ToString()));
    }

    private Dispute ArrangeDispute(string ownerUserId)
    {
        // The dispute is filed by ownerUserId; orderId/reason/description are irrelevant here.
        var dispute = new Dispute(
            orderId: "order-1",
            userId: ownerUserId,
            reason: DisputeReason.Other,
            description: "x",
            createdBy: ownerUserId)
        {
            Id = DisputeId,
        };
        _disputeRepository
            .Setup(r => r.GetDisputeWithDetailsAsync(DisputeId))
            .ReturnsAsync(dispute);
        return dispute;
    }

    // ── AC1 — customer self-reply ownership ([OWN-DATA]) ───────────────────

    [Fact]
    public async Task Customer_Messaging_Own_Dispute_Succeeds_As_Customer_Message()
    {
        var dispute = ArrangeDispute(ownerUserId: CustomerSub);
        SetCaller(CustomerSub, UserProfile.Customer);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddDisputeMessage.Command(DisputeId, "hi", IsStaffMessage: false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var message = Assert.Single(dispute.Messages);
        Assert.False(message.IsStaffMessage);
        Assert.Equal(CustomerSub, message.AuthorId);
        // Customer-authored message must NOT notify anyone.
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(), It.IsAny<QueueEnvelope<SendPushNotificationMessage>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Customer_Messaging_Another_Customers_Dispute_Is_Denied()
    {
        ArrangeDispute(ownerUserId: OtherCustomerSub);
        SetCaller(CustomerSub, UserProfile.Customer);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddDisputeMessage.Command(DisputeId, "hi", IsStaffMessage: false),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeNotOwnedByUser, result.Error!.Message);
    }

    // ── AC3 — staff flag is server-derived from profile, never the body ────

    [Fact]
    public async Task Customer_Forging_IsStaffMessage_True_Is_Recorded_As_Customer_Message()
    {
        // The hole: a customer-host caller flips IsStaffMessage=true in the body. The handler must
        // derive the flag from the caller's profile (a customer is never staff) — the forged message
        // is recorded as a CUSTOMER message (isStaff=false), still scoped to their OWN dispute, and
        // no staff→customer push is sent.
        var dispute = ArrangeDispute(ownerUserId: CustomerSub);
        SetCaller(CustomerSub, UserProfile.Customer);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddDisputeMessage.Command(DisputeId, "forged", IsStaffMessage: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var message = Assert.Single(dispute.Messages);
        Assert.False(message.IsStaffMessage);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(), It.IsAny<QueueEnvelope<SendPushNotificationMessage>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Employee_Cannot_Produce_A_Staff_Message_Even_With_Flag_Set()
    {
        // An Employee is not Admin — staff replies are Admin-only (Q-0005). Even if the flag is set,
        // the message is not staff; and being a non-owner non-staff, the ownership check denies it.
        ArrangeDispute(ownerUserId: CustomerSub);
        SetCaller("employee-sub-3", UserProfile.Employee);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddDisputeMessage.Command(DisputeId, "x", IsStaffMessage: true),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeNotOwnedByUser, result.Error!.Message);
        _pending.Verify(p => p.Enqueue(
            It.IsAny<string>(), It.IsAny<QueueEnvelope<SendPushNotificationMessage>>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Admin_Staff_Reply_Is_Recorded_As_Staff_And_Pushes_To_Customer()
    {
        // The legitimate staff path (Admin host, CanRespondToDispute) is unchanged in behavior:
        // the message is staff and the customer is notified.
        var dispute = ArrangeDispute(ownerUserId: CustomerSub);
        SetCaller(AdminSub, UserProfile.Administrator);
        var handler = CreateHandler();

        var result = await handler.Handle(
            new AddDisputeMessage.Command(DisputeId, "we are looking into it", IsStaffMessage: true),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var message = Assert.Single(dispute.Messages);
        Assert.True(message.IsStaffMessage);
        Assert.Equal(AdminSub, message.AuthorId);
        // The staff→customer push is now RECORDED (post-commit dispatch) wrapped in a QueueEnvelope
        // with the frozen push key push:{UserId}:{EventKey}:{disputeId}.
        _pending.Verify(p => p.Enqueue(
            QueueNames.NotificationsDispatch,
            It.Is<QueueEnvelope<SendPushNotificationMessage>>(e =>
                e.Payload.UserId == CustomerSub
                && e.MessageKey == MessageKeys.Push(CustomerSub, NotificationEventCatalog.DisputeReply, DisputeId)),
            MessageKeys.Push(CustomerSub, NotificationEventCatalog.DisputeReply, DisputeId)),
            Times.Once);
    }
}
