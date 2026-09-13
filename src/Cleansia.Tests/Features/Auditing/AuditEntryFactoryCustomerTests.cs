using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0062 D1 — the customer row's shape as the factory assembles it: subject from the session (S1: the
/// session wins over the snapshot's <c>ActorUserId</c>), <c>ClientAudience</c> from the host that served
/// the request (filled on an anonymous row), IP/device from the request, payload from the evidence
/// snapshot, and the resource id from the snapshot or the EXACT <c>{ResourceType}Id</c> property only.
/// </summary>
public sealed class AuditEntryFactoryCustomerTests
{
    [AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ResourceType = "Order")]
    public sealed record CancelCommand(string OrderId);

    private static readonly AuditActionDescriptor CancelDescriptor = AuditActionDescriptor.For(typeof(CancelCommand));

    private static IUserSessionProvider CustomerSession(string userId) =>
        new TestUserSessionProvider(userId, $"{userId}@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);

    private static IUserSessionProvider AnonymousSession() => new TestUserSessionProvider([]);

    private static AuditEntryFactory Factory(
        IUserSessionProvider session,
        string audience = JwtAudiences.Customer,
        IRequestMetadataProvider? metadata = null) =>
        new(session, metadata ?? new TestRequestMetadataProvider(), new HostAudienceProvider(audience));

    [Fact]
    public void A_Success_Row_Carries_The_Session_User_Host_Audience_Request_Context_And_The_Evidence_Payload()
    {
        var context = new AuditContext();
        context.RecordEvidence("Order", "ORD-1", new { feeRate = 0.5m, hasBeenAccepted = true });
        var factory = Factory(
            CustomerSession("cust-1"),
            metadata: new TestRequestMetadataProvider("203.0.113.9", "iPhone 15 / 17.4", "device-abc"));

        var row = factory.CreateCustomerSuccess(new CancelCommand("ORD-1"), CancelDescriptor, context.DrainSnapshot());

        Assert.True(row.Success);
        Assert.Null(row.ErrorCode);
        Assert.Equal("cust-1", row.UserId);
        Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
        Assert.Equal("203.0.113.9", row.IpAddress);
        Assert.Equal("iPhone 15 / 17.4", row.DeviceLabel);
        Assert.Equal("device-abc", row.DeviceId);
        Assert.Equal("customer.order.cancel", row.Action);
        Assert.Equal("Order", row.ResourceType);
        Assert.Equal("ORD-1", row.ResourceId);
        Assert.Equal("{\"feeRate\":0.5,\"hasBeenAccepted\":true}", row.PayloadJson);
        Assert.InRange(row.OccurredOn, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public void The_Session_User_Wins_Over_The_Snapshots_ActorUserId()
    {
        var context = new AuditContext();
        context.RecordEvidence("User", "new-user-9", new { method = "email" }, actorUserId: "new-user-9");

        var row = Factory(CustomerSession("cust-1"))
            .CreateCustomerSuccess(new CancelCommand("ORD-1"), CancelDescriptor, context.DrainSnapshot());

        Assert.Equal("cust-1", row.UserId);
    }

    [Fact]
    public void An_Anonymous_Row_Takes_The_Snapshots_ActorUserId_And_Still_Carries_The_Host_Audience()
    {
        var context = new AuditContext();
        context.RecordEvidence("User", "new-user-9", new { method = "email" }, actorUserId: "new-user-9");

        var row = Factory(AnonymousSession(), audience: JwtAudiences.Mobile)
            .CreateCustomerSuccess(new CancelCommand("ORD-1"), CancelDescriptor, context.DrainSnapshot());

        Assert.Equal("new-user-9", row.UserId);
        Assert.Equal(JwtAudiences.Mobile, row.ClientAudience);
    }

    [Fact]
    public void An_Anonymous_Row_With_No_ActorUserId_Is_A_Guest_Act_With_A_Null_UserId()
    {
        var row = Factory(AnonymousSession())
            .CreateCustomerSuccess(new CancelCommand("ORD-1"), CancelDescriptor, snapshot: null);

        Assert.Null(row.UserId);
        Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
    }

    [Fact]
    public void The_Snapshot_Relabels_The_Resource_When_The_Handler_Recorded_A_Different_One()
    {
        var context = new AuditContext();
        context.RecordEvidence("Dispute", "DSP-7", new { orderId = "ORD-1", descriptionLength = 42 });

        var row = Factory(CustomerSession("cust-1"))
            .CreateCustomerSuccess(new CancelCommand("ORD-1"), CancelDescriptor, context.DrainSnapshot());

        Assert.Equal("Dispute", row.ResourceType);
        Assert.Equal("DSP-7", row.ResourceId);
    }

    [Fact]
    public void A_Failure_Row_Records_The_Key_The_Exact_Resource_Id_And_No_Payload()
    {
        var row = Factory(CustomerSession("cust-1"))
            .CreateCustomerFailure(new CancelCommand("ORD-1"), CancelDescriptor, BusinessErrorMessage.OrderInProgressCannotCancel);

        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, row.ErrorCode);
        Assert.Equal("Order", row.ResourceType);
        Assert.Equal("ORD-1", row.ResourceId);
        Assert.Null(row.PayloadJson);
        Assert.Equal("cust-1", row.UserId);
    }

    /// <summary>
    /// The ticket's own acceptance criterion: a refused checkout must not carry the country id labelled
    /// as the membership. Uses the REAL command type so a future shape change re-checks it.
    /// </summary>
    [Fact]
    public void A_Failed_CreateMembershipCheckoutSession_Row_Has_A_Null_ResourceId_Never_The_CountryId()
    {
        var descriptor = new AuditActionDescriptor(
            "customer.membership.subscribe", "UserMembership", Sensitive: false, Audited: true, AuditAudience.Customer);

        var row = Factory(CustomerSession("cust-1"))
            .CreateCustomerFailure(new CreateMembershipCheckoutSession.Command("plus", "CZ"), descriptor, BusinessErrorMessage.Required);

        Assert.Equal("UserMembership", row.ResourceType);
        Assert.Null(row.ResourceId);
    }

    [Fact]
    public void A_Malformed_Client_Sent_Id_Is_Clamped_To_The_Column_So_The_Probe_Row_Still_Lands()
    {
        var probe = new string('z', 200);

        var row = Factory(CustomerSession("cust-1"))
            .CreateCustomerFailure(new CancelCommand(probe), CancelDescriptor, BusinessErrorMessage.OrderNotFound);

        Assert.Equal(CustomerActionAudit.ResourceIdMaxLength, row.ResourceId!.Length);
        Assert.Equal(probe[..CustomerActionAudit.ResourceIdMaxLength], row.ResourceId);
    }

    [Fact]
    public void The_Admin_Pair_Is_Untouched_By_The_Customer_Collaborators()
    {
        var session = new TestUserSessionProvider("admin-1", "admin@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);

        var row = Factory(session, audience: JwtAudiences.Admin, metadata: new TestRequestMetadataProvider("203.0.113.9"))
            .CreateFailure(new CancelCommand("ORD-1"), CancelDescriptor, BusinessErrorMessage.OrderNotFound);

        Assert.Equal("admin-1", row.ActorId);
        Assert.Equal(UserProfile.Administrator, row.ActorProfile);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, row.ErrorCode);
    }
}
