using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Dispute add-message write-path TENANT isolation (testing.md must-cover #5, S3/S8) on the Customer
/// host. Ac6 already locks the same-tenant cross-USER rejection (DisputeNotOwnedByUser) and the owner
/// happy path; Ac9 locks cross-tenant Dispute CREATE. This rounds out the dispute WRITE path with the
/// remaining boundary — cross-tenant ADD-MESSAGE: the AddDisputeMessage validator's existence check
/// runs through the tenant-filtered <c>IDisputeRepository.ExistsAsync</c>, so a foreign-tenant caller
/// (even with the genuine owner's sub) never sees the dispute → <c>DisputeNotFound</c>, and no message
/// is appended. Both tenants carry NON-NULL distinct tenant_id claims so the multi-tenant filter branch
/// is the one under test.
/// </summary>
public sealed class Ac12CrossTenantDisputeMessageWriteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    private sealed record Arranged(string OwnerId, string OwnerEmail, string DisputeId);

    private async Task<Arranged> ArrangeOwnedDisputeInTenantAAsync()
    {
        string ownerId = "", disputeId = "";
        const string ownerEmail = "xt-dispmsg-owner@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(ownerEmail, tenantId: TenantA);
            ctx.Users.Add(owner);
            var order = DomainSeed.NewOrder(owner.Id, ownerEmail, tenantId: TenantA);
            ctx.Orders.Add(order);
            var dispute = DomainSeed.Dispute(order.Id, owner.Id, tenantId: TenantA);
            ctx.Disputes.Add(dispute);
            ownerId = owner.Id;
            disputeId = dispute.Id;
        });
        return new Arranged(ownerId, ownerEmail, disputeId);
    }

    private static HttpContent MessageBody(string disputeId) => JsonContent.Create(new
    {
        DisputeId = disputeId,
        Message = "cross-tenant dispute message attempt",
        IsStaffMessage = false,
    });

    [Fact]
    public async Task Cross_tenant_add_dispute_message_returns_not_found_and_appends_no_message()
    {
        var a = await ArrangeOwnedDisputeInTenantAAsync();
        // sub = the genuine dispute owner (so the inner ownership gate would pass); token tenant is B.
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantB);

        var resp = await CustomerClient(token).PostAsync("/api/Dispute/AddMessage", MessageBody(a.DisputeId));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.DisputeNotFound);

        var messageCount = await QueryAsync(ctx => ctx.Set<DisputeMessage>()
            .IgnoreQueryFilters().CountAsync(m => m.DisputeId == a.DisputeId));
        Assert.Equal(0, messageCount);
    }

    [Fact]
    public async Task In_tenant_owner_add_dispute_message_succeeds_and_appends_the_message()
    {
        var a = await ArrangeOwnedDisputeInTenantAAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantA);

        var resp = await CustomerClient(token).PostAsync("/api/Dispute/AddMessage", MessageBody(a.DisputeId));

        HttpAssert.IsOk(resp);

        var messageCount = await QueryAsync(ctx => ctx.Set<DisputeMessage>()
            .IgnoreQueryFilters().CountAsync(m => m.DisputeId == a.DisputeId && m.AuthorId == a.OwnerId));
        Assert.Equal(1, messageCount);
    }
}
