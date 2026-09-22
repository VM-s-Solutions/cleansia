using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// A customer's dispute messages follow proven ownership across operators. Another customer's id,
/// including one in the same account company, gets the same not-found refusal as a missing dispute.
/// </summary>
public sealed class Ac12CrossTenantDisputeMessageWriteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = HostTestTenants.A;
    private const string TenantB = HostTestTenants.B;

    private sealed record Arranged(string OwnerId, string OwnerEmail, string OutsiderId, string DisputeId, string OrderId);

    private async Task<Arranged> ArrangeOwnedDisputeInTenantAAsync(string ownerTenant = TenantB)
    {
        string ownerId = "", outsiderId = "", disputeId = "", orderId = "";
        const string ownerEmail = "xt-dispmsg-owner@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(ownerEmail, tenantId: ownerTenant);
            var outsider = DomainSeed.Customer("xt-dispmsg-outsider@hosttests.local", tenantId: TenantB);
            ctx.Users.AddRange(owner, outsider);
            var order = DomainSeed.NewOrder(owner.Id, ownerEmail, tenantId: TenantA);
            ctx.Orders.Add(order);
            var dispute = DomainSeed.Dispute(order.Id, owner.Id, tenantId: TenantA);
            ctx.Disputes.Add(dispute);
            ownerId = owner.Id;
            outsiderId = outsider.Id;
            disputeId = dispute.Id;
            orderId = order.Id;
        });
        return new Arranged(ownerId, ownerEmail, outsiderId, disputeId, orderId);
    }

    private static HttpContent MessageBody(string disputeId) => JsonContent.Create(new
    {
        DisputeId = disputeId,
        Message = "cross-tenant dispute message attempt",
        IsStaffMessage = false,
    });

    [Fact]
    public async Task Cross_market_owner_can_reply_and_another_customer_cannot_append_a_message()
    {
        var a = await ArrangeOwnedDisputeInTenantAAsync();
        var ownerToken = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail, UserProfile.Customer, tenantId: TenantB);
        HttpAssert.IsOk(await CustomerClient(ownerToken).PostAsync("/api/Dispute/AddMessage", MessageBody(a.DisputeId)));
        var outsiderToken = TestJwtFactory.Mint(CustomerAudience, a.OutsiderId, "xt-dispmsg-outsider@hosttests.local", UserProfile.Customer, tenantId: TenantB);
        foreach (var disputeId in new[] { a.DisputeId, "missing-dispute" })
        {
            var response = await CustomerClient(outsiderToken).PostAsync("/api/Dispute/AddMessage", MessageBody(disputeId));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await HttpAssert.AssertBusinessErrorAsync(response, BusinessErrorMessage.DisputeNotFound);
        }
        var messages = await QueryAsync(ctx => ctx.Set<DisputeMessage>().IgnoreQueryFilters().Where(m => m.DisputeId == a.DisputeId).ToListAsync());
        var message = Assert.Single(messages);
        Assert.Equal(a.OwnerId, message.AuthorId);
        Assert.False(message.IsStaffMessage);
        Assert.Equal("cross-tenant dispute message attempt", message.Message);
        var dispute = await QueryAsync(ctx => ctx.Disputes.IgnoreQueryFilters().SingleAsync(d => d.Id == a.DisputeId));
        Assert.Equal(TenantA, dispute.TenantId);
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
        Assert.Equal(a.OwnerId, dispute.UserId);
        Assert.Equal(TenantA, await QueryAsync(ctx => ctx.Orders.IgnoreQueryFilters().Where(o => o.Id == a.OrderId).Select(o => o.TenantId).SingleAsync()));
        Assert.Empty(await QueryAsync(ctx => ctx.OutboxMessages.IgnoreQueryFilters().ToListAsync()));
    }

    [Fact]
    public async Task In_tenant_owner_add_dispute_message_succeeds_and_appends_the_message()
    {
        var a = await ArrangeOwnedDisputeInTenantAAsync(TenantA);
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantA);

        var resp = await CustomerClient(token).PostAsync("/api/Dispute/AddMessage", MessageBody(a.DisputeId));

        HttpAssert.IsOk(resp);

        var messageCount = await QueryAsync(ctx => ctx.Set<DisputeMessage>()
            .IgnoreQueryFilters().CountAsync(m => m.DisputeId == a.DisputeId && m.AuthorId == a.OwnerId));
        Assert.Equal(1, messageCount);
    }
}
