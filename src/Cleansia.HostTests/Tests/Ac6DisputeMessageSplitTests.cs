using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// The dispute message split + server-derived
/// staff flag:
/// <list type="number">
///   <item>a Customer replying to their OWN dispute (Customer host CanAddDisputeMessage) → 200, and the
///   message is recorded as a CUSTOMER message even if the body sets IsStaffMessage=true (the controller
///   forces it false and the handler re-derives it from the caller's profile);</item>
///   <item>a Customer replying to ANOTHER customer's dispute → denied (DisputeNotOwnedByUser);</item>
///   <item>a Customer hitting the Admin staff-reply endpoint (CanRespondToDispute = AdminOnly) → 403.</item>
/// </list>
/// </summary>
public sealed class Ac6DisputeMessageSplitTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record DisputeArranged(
        string OwnerId, string OwnerEmail, string OutsiderId, string OutsiderEmail, string DisputeId);

    private async Task<DisputeArranged> ArrangeOwnedDisputeAsync()
    {
        string ownerId = "", outsiderId = "", disputeId = "";
        const string ownerEmail = "dispowner@hosttests.local";
        const string outsiderEmail = "outsider@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(ownerEmail);
            var outsider = DomainSeed.Customer(outsiderEmail);
            ctx.Users.AddRange(owner, outsider);

            var order = DomainSeed.NewOrder(owner.Id, ownerEmail);
            ctx.Orders.Add(order);

            var dispute = DomainSeed.Dispute(order.Id, owner.Id);
            ctx.Disputes.Add(dispute);

            ownerId = owner.Id;
            outsiderId = outsider.Id;
            disputeId = dispute.Id;
        });

        return new DisputeArranged(ownerId, ownerEmail, outsiderId, outsiderEmail, disputeId);
    }

    private static HttpContent MessageBody(string disputeId, bool claimStaff) => JsonContent.Create(new
    {
        DisputeId = disputeId,
        Message = "host-test customer reply",
        IsStaffMessage = claimStaff,
    });

    [Fact]
    public async Task Customer_self_reply_succeeds_and_is_recorded_as_a_customer_message_even_if_body_claims_staff()
    {
        var a = await ArrangeOwnedDisputeAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail, UserProfile.Customer);

        // Body lies: IsStaffMessage=true. The server must derive it from the caller profile (customer).
        var resp = await CustomerClient(token).PostAsync("/api/Dispute/AddMessage", MessageBody(a.DisputeId, claimStaff: true));

        HttpAssert.IsOk(resp);

        var message = await QueryAsync(ctx => ctx.Set<DisputeMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.DisputeId == a.DisputeId && m.AuthorId == a.OwnerId)
            .FirstOrDefaultAsync());

        Assert.NotNull(message);
        Assert.False(message!.IsStaffMessage); // recorded as a CUSTOMER message despite the body claim
    }

    [Fact]
    public async Task Customer_replying_to_another_customers_dispute_is_denied()
    {
        var a = await ArrangeOwnedDisputeAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OutsiderId, a.OutsiderEmail, UserProfile.Customer);

        var resp = await CustomerClient(token).PostAsync("/api/Dispute/AddMessage", MessageBody(a.DisputeId, claimStaff: false));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.DisputeNotOwnedByUser);
    }

    [Fact]
    public async Task Customer_hitting_the_admin_staff_reply_endpoint_is_403d()
    {
        var a = await ArrangeOwnedDisputeAsync();
        // Admin-audience token (so it authenticates on the Admin host) but carrying the Customer role:
        // the CanRespondToDispute = AdminOnly policy must 403 it.
        var token = TestJwtFactory.Mint(AdminAudience, a.OutsiderId, a.OutsiderEmail, UserProfile.Customer);

        var resp = await AdminClient(token).PostAsync("/api/AdminDispute/add-message", MessageBody(a.DisputeId, claimStaff: true));

        HttpAssert.IsForbidden(resp);
    }
}
