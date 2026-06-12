using System.Net.Http.Json;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Admin dispute management lives on the Admin host (AdminDisputeController), gated by its existing
/// AdminOnly policies. End-to-end:
/// <list type="bullet">
///   <item>UpdateStatus / Resolve / GetPaged are AdminOnly — a non-admin caller (right Admin audience,
///   Employee/Customer role) is 403'd at the [Permission] gate and a status write never reaches the
///   handler;</item>
///   <item>the Admin-role companion clears the same gate, proving it is genuinely enforced;</item>
///   <item>the Partner host no longer exposes Resolve/UpdateStatus at all (SEC-DSP-07) — those routes
///   404.</item>
/// </list>
/// </summary>
public sealed class Ac10DisputeUpdateStatusAdminOnlyTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record DisputeArranged(string OwnerId, string OwnerEmail, string DisputeId);

    private async Task<DisputeArranged> ArrangeDisputeAsync()
    {
        string ownerId = "", disputeId = "";
        const string ownerEmail = "disp-status-owner@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(ownerEmail);
            ctx.Users.Add(owner);

            var order = DomainSeed.NewOrder(owner.Id, ownerEmail);
            ctx.Orders.Add(order);

            var dispute = DomainSeed.Dispute(order.Id, owner.Id);
            ctx.Disputes.Add(dispute);

            ownerId = owner.Id;
            disputeId = dispute.Id;
        });

        return new DisputeArranged(ownerId, ownerEmail, disputeId);
    }

    private static HttpContent UpdateStatusBody(string disputeId) => JsonContent.Create(new
    {
        DisputeId = disputeId,
        NewStatus = DisputeStatus.UnderReview, // a legal edge from Pending — so a 403 is the gate, not the guard
    });

    private static HttpContent ResolveBody(string disputeId) => JsonContent.Create(new
    {
        DisputeId = disputeId,
        RefundAmount = (decimal?)null,
        ResolutionNotes = "host-test resolution",
    });

    [Fact]
    public async Task NonAdmin_caller_is_403d_on_Admin_UpdateStatus_and_status_is_untouched()
    {
        var a = await ArrangeDisputeAsync();
        // Admin audience (authenticates on the Admin host) carrying the Employee role: the
        // CanUpdateDisputeStatus = AdminOnly policy must 403 it.
        var token = TestJwtFactory.Mint(AdminAudience, a.OwnerId, a.OwnerEmail, UserProfile.Employee);

        var resp = await AdminClient(token).PostAsync("/api/AdminDispute/update-status", UpdateStatusBody(a.DisputeId));

        HttpAssert.IsForbidden(resp);

        var dispute = await QueryAsync(ctx => ctx.Set<Dispute>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == a.DisputeId));
        Assert.NotNull(dispute);
        Assert.Equal(DisputeStatus.Pending, dispute!.Status); // unchanged — the write never reached the handler
    }

    [Fact]
    public async Task NonAdmin_caller_is_403d_on_Admin_Resolve()
    {
        var a = await ArrangeDisputeAsync();
        var token = TestJwtFactory.Mint(AdminAudience, a.OwnerId, a.OwnerEmail, UserProfile.Customer);

        var resp = await AdminClient(token).PostAsync("/api/AdminDispute/resolve", ResolveBody(a.DisputeId));

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task NonAdmin_caller_is_403d_on_Admin_GetPaged()
    {
        var a = await ArrangeDisputeAsync();
        var token = TestJwtFactory.Mint(AdminAudience, a.OwnerId, a.OwnerEmail, UserProfile.Employee);

        var resp = await AdminClient(token).GetAsync("/api/AdminDispute/get-paged");

        HttpAssert.IsForbidden(resp);
    }

    [Fact]
    public async Task Admin_caller_clears_the_Admin_UpdateStatus_gate()
    {
        var a = await ArrangeDisputeAsync();
        var token = TestJwtFactory.Mint(AdminAudience, "admin-disp-status", "admin-disp-status@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).PostAsync("/api/AdminDispute/update-status", UpdateStatusBody(a.DisputeId));

        // The admin token is NOT denied at the auth/authz layer — it reaches the handler.
        HttpAssert.IsOk(resp);
    }

    [Fact]
    public async Task Admin_caller_clears_the_Admin_GetPaged_gate()
    {
        await ArrangeDisputeAsync();
        var token = TestJwtFactory.Mint(AdminAudience, "admin-disp-list", "admin-disp-list@hosttests.local", UserProfile.Administrator);

        var resp = await AdminClient(token).GetAsync("/api/AdminDispute/get-paged");

        HttpAssert.IsOk(resp);
    }

    [Fact]
    public async Task Partner_host_no_longer_exposes_UpdateStatus_or_Resolve()
    {
        var a = await ArrangeDisputeAsync();
        var token = TestJwtFactory.Mint(PartnerAudience, "admin-on-partner", "admin-on-partner@hosttests.local", UserProfile.Administrator);

        var updateStatus = await PartnerClient(token).PostAsync("/api/Dispute/UpdateStatus", UpdateStatusBody(a.DisputeId));
        var resolve = await PartnerClient(token).PostAsync("/api/Dispute/Resolve", ResolveBody(a.DisputeId));

        HttpAssert.IsNotFound(updateStatus);
        HttpAssert.IsNotFound(resolve);
    }
}
