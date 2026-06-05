using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// AC5 (SEC-DSP-02, paired fix T-0103) — a customer can only open a dispute on an order they OWN.
/// CanCreateDispute is CustomerOnly (coarse gate) + an inner handler ownership check loaded via the
/// tenant-filtered GetByIdAsync ([OWN-DATA], S3/S8). End-to-end on the Customer host:
/// <list type="bullet">
///   <item>a customer who does NOT own order X → the ownership/not-found business error AND no Dispute
///   row is persisted;</item>
///   <item>the owner disputing their own order → 200 and a Dispute row exists.</item>
/// </list>
/// </summary>
public sealed class Ac5DisputeOrderOwnershipTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record OrderArranged(string AttackerId, string AttackerEmail, string OwnerId, string OwnerEmail, string OrderId);

    private async Task<OrderArranged> ArrangeOrderOwnedByAnotherAsync()
    {
        string attackerId = "", ownerId = "", orderId = "";
        const string attackerEmail = "attacker@hosttests.local";
        const string ownerEmail = "owner@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var attacker = DomainSeed.Customer(attackerEmail);
            var owner = DomainSeed.Customer(ownerEmail);
            ctx.Users.AddRange(attacker, owner);

            var order = DomainSeed.NewOrder(owner.Id, ownerEmail);
            ctx.Orders.Add(order);

            attackerId = attacker.Id;
            ownerId = owner.Id;
            orderId = order.Id;
        });

        return new OrderArranged(attackerId, attackerEmail, ownerId, ownerEmail, orderId);
    }

    private static HttpContent DisputeBody(string orderId) => JsonContent.Create(new
    {
        OrderId = orderId,
        Reason = (int)DisputeReason.Other,
        Description = "host-test dispute description body",
    });

    [Fact]
    public async Task Customer_disputing_an_order_they_do_not_own_is_rejected_and_no_dispute_is_created()
    {
        var a = await ArrangeOrderOwnedByAnotherAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.AttackerId, a.AttackerEmail, UserProfile.Customer);

        var resp = await CustomerClient(token).PostAsync("/api/Dispute/Create", DisputeBody(a.OrderId));

        await HttpAssert.RejectedAsync(resp, BusinessErrorMessage.OrderNotFound);

        var disputeCount = await QueryAsync(ctx =>
            ctx.Set<Dispute>().IgnoreQueryFilters().CountAsync(d => d.OrderId == a.OrderId));
        Assert.Equal(0, disputeCount);
    }

    [Fact]
    public async Task Owner_disputing_their_own_order_succeeds_and_a_dispute_is_created()
    {
        var a = await ArrangeOrderOwnedByAnotherAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail, UserProfile.Customer);

        var resp = await CustomerClient(token).PostAsync("/api/Dispute/Create", DisputeBody(a.OrderId));

        HttpAssert.IsOk(resp);

        var disputeCount = await QueryAsync(ctx =>
            ctx.Set<Dispute>().IgnoreQueryFilters().CountAsync(d => d.OrderId == a.OrderId && d.UserId == a.OwnerId));
        Assert.Equal(1, disputeCount);
    }
}
