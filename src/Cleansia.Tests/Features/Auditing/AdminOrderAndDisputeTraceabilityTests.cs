using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// Q-AUD-O2 (owner ruling): "the reason is worth nothing if I can't trace the failed order". Every admin
/// command against an order or a dispute resolves its resource type AND id off the command alone, so the
/// out-of-band failure row — which has no snapshot — still names the order or dispute the admin was
/// refused on, and the timeline by resource finds it. Two of these used to resolve nothing:
/// <c>AdminReassignOrder</c> carries three ids (the single-*Id fallback answers none), and the unmarked
/// commands carried no resource type at all, which the timeline filters on.
/// </summary>
public sealed class AdminOrderAndDisputeTraceabilityTests
{
    private const string OrderId = "order-under-test";
    private const string DisputeId = "dispute-under-test";

    public static TheoryData<object, string, string, string> Commands => new()
    {
        { new AdminCancelOrder.Command(OrderId, "reason"), "order.cancel", "Order", OrderId },
        { new AdminReassignOrder.Command(OrderId, "employee-from", "employee-to"), "order.reassign", "Order", OrderId },
        { new AdminOverrideOrderStatus.Command(OrderId, OrderStatus.Completed), "order.status.override", "Order", OrderId },
        { new AdminRefundOrder.Command(OrderId), "order.refund.full", "Order", OrderId },
        { new RevealOrderAccessInstructions.Command(OrderId), "order.access_instructions.reveal", "Order", OrderId },
        { new IssuePartialRefund.Command(OrderId, [], RefundReason.AdminDiscretion, null), "order.refund.partial", "Order", OrderId },
        { new ResolveDispute.Command(DisputeId, null, "notes"), "dispute.resolve", "Dispute", DisputeId },
        { new UpdateDisputeStatus.Command(DisputeId, DisputeStatus.UnderReview), "dispute.status.update", "Dispute", DisputeId },
        { new AddDisputeMessage.Command(DisputeId, "message", IsStaffMessage: true), "dispute.message.add", "Dispute", DisputeId },
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void Every_Admin_Order_And_Dispute_Command_Resolves_Its_Resource_Off_The_Command_Alone(
        object command, string label, string resourceType, string resourceId)
    {
        var descriptor = AuditActionDescriptor.For(command.GetType());

        Assert.Equal(label, descriptor.Action);
        Assert.Equal(resourceType, descriptor.ResourceType);
        Assert.Equal(resourceId, AuditResourceResolver.ResolveResourceId(command, descriptor.ResourceType));
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void The_Failure_Row_Names_The_Resource_And_The_Key(object command, string label, string resourceType, string resourceId)
    {
        var session = new TestUserSessionProvider("admin-1", "admin@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);
        var factory = new AuditEntryFactory(session, new TestRequestMetadataProvider(), new HostAudienceProvider(JwtAudiences.Admin));

        var row = factory.CreateFailure(command, AuditActionDescriptor.For(command.GetType()), BusinessErrorMessage.OrderInProgressCannotCancel);

        Assert.False(row.Success);
        Assert.Equal(label, row.Action);
        Assert.Equal(resourceType, row.ResourceType);
        Assert.Equal(resourceId, row.ResourceId);
        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, row.ErrorCode);
        Assert.Equal("admin-1", row.ActorId);
    }
}
