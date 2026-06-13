using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Memberships;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// UserMembership write-path isolation (testing.md must-cover #5, S8) on the Customer host. The
/// membership write endpoints (<c>Cancel</c>/<c>SwapPlan</c>) resolve the caller's membership SOLELY
/// from the authenticated subject via the tenant-filtered <c>GetActiveForUserAsync</c> — there is no
/// request-supplied membership id, so the cross-USER boundary degenerates to "a different subject
/// resolves a different (or no) membership". We assert both forms of "you cannot reach another party's
/// membership":
/// <list type="bullet">
///   <item>a foreign-TENANT caller (sub = the genuine owner, token tenant differs) → the tenant filter
///   hides the row → <c>MembershipNotFound</c>;</item>
///   <item>a same-tenant DIFFERENT user (no membership of their own) → <c>MembershipNotFound</c>.</item>
/// </list>
/// Both rejection paths short-circuit BEFORE any Stripe call, so the harness (full production pipeline,
/// no Stripe stub) exercises them honestly. The legitimate-owner success path goes on to call Stripe
/// (<c>CancelSubscriptionAtPeriodEnd</c>) which the host harness does not stub — that happy path is
/// proven at the unit layer (CancelMembershipSubscription handler test); here the in-tenant owner
/// READING their membership (GetMine → 200) demonstrates the row is reachable by its owner, so the
/// write rejections are the boundary, not an unreachable resource. Both tenants carry NON-NULL distinct
/// tenant_id claims so the multi-tenant filter branch is under test.
/// </summary>
public sealed class Ac13CrossTenantMembershipWriteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    private sealed record Arranged(string OwnerId, string OwnerEmail);

    private async Task<Arranged> ArrangeActiveMembershipInTenantAAsync()
    {
        string ownerId = "";
        const string ownerEmail = "mem-owner@hosttests.local";
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(ownerEmail, tenantId: TenantA);
            ctx.Users.Add(owner);
            var plan = DomainSeed.MembershipPlan(tenantId: TenantA);
            ctx.MembershipPlans.Add(plan);
            var membership = DomainSeed.ActiveMembership(owner.Id, plan.Id, tenantId: TenantA);
            ctx.UserMemberships.Add(membership);
            ownerId = owner.Id;
        });
        return new Arranged(ownerId, ownerEmail);
    }

    [Fact]
    public async Task Cross_tenant_cancel_membership_returns_not_found_and_leaves_the_membership_active()
    {
        var a = await ArrangeActiveMembershipInTenantAAsync();
        // sub = the genuine membership owner; only the token's tenant differs.
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantB);

        var resp = await CustomerClient(token).PostAsync("/api/Membership/Cancel", null);

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.MembershipNotFound);

        var stillActive = await QueryAsync(ctx => ctx.Set<UserMembership>()
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == a.OwnerId && m.Status == MembershipStatus.Active && m.CancelledAt == null));
        Assert.True(stillActive);
    }

    [Fact]
    public async Task Cross_user_same_tenant_cancel_membership_returns_not_found_and_leaves_the_owners_membership_active()
    {
        var a = await ArrangeActiveMembershipInTenantAAsync();
        string outsiderId = "";
        const string outsiderEmail = "mem-outsider@hosttests.local";
        await SeedAsync(async ctx =>
        {
            var outsider = DomainSeed.Customer(outsiderEmail, tenantId: TenantA);
            ctx.Users.Add(outsider);
            outsiderId = outsider.Id;
        });

        // Same tenant, but a different subject with no membership of their own.
        var token = TestJwtFactory.Mint(CustomerAudience, outsiderId, outsiderEmail,
            UserProfile.Customer, tenantId: TenantA);

        var resp = await CustomerClient(token).PostAsync("/api/Membership/Cancel", null);

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.MembershipNotFound);

        var stillActive = await QueryAsync(ctx => ctx.Set<UserMembership>()
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == a.OwnerId && m.Status == MembershipStatus.Active && m.CancelledAt == null));
        Assert.True(stillActive);
    }

    [Fact]
    public async Task In_tenant_owner_can_reach_their_membership()
    {
        var a = await ArrangeActiveMembershipInTenantAAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail,
            UserProfile.Customer, tenantId: TenantA);

        var resp = await CustomerClient(token).GetAsync("/api/Membership/GetMine");

        HttpAssert.IsOk(resp);
    }
}
