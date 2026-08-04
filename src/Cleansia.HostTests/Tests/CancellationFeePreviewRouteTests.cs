using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// T-0526 — the cancellation preview end-to-end on the Customer host: the real route, the real
/// <c>CanCancelOrder</c> policy, the real handler. Three claims the unit suites cannot make:
/// <list type="bullet">
///   <item>the owner gets a quote (the endpoint is reachable and its query binds from the URL);</item>
///   <item>another customer gets the same <c>order.not_found</c> a missing order returns, so the
///   preview is not an existence oracle for other people's bookings (S3);</item>
///   <item>calling it leaves the order exactly as it was — a read, through the whole pipeline
///   including the unit-of-work behavior (AC5).</item>
/// </list>
/// </summary>
public sealed class CancellationFeePreviewRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private sealed record Arranged(string OwnerId, string OwnerEmail, string OutsiderId, string OutsiderEmail, string OrderId);

    private async Task<Arranged> ArrangeOrderAsync()
    {
        string ownerId = "", outsiderId = "", orderId = "";
        const string ownerEmail = "preview-owner@hosttests.local";
        const string outsiderEmail = "preview-outsider@hosttests.local";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var owner = DomainSeed.Customer(ownerEmail);
            var outsider = DomainSeed.Customer(outsiderEmail);
            ctx.Users.AddRange(owner, outsider);

            var order = DomainSeed.NewOrder(owner.Id, ownerEmail);
            ctx.Orders.Add(order);

            ownerId = owner.Id;
            outsiderId = outsider.Id;
            orderId = order.Id;
        });

        return new Arranged(ownerId, ownerEmail, outsiderId, outsiderEmail, orderId);
    }

    private static string Route(string orderId) => $"/api/Order/CancellationPreview?orderId={orderId}";

    [Fact]
    public async Task Owner_gets_a_quote_for_their_own_order()
    {
        var a = await ArrangeOrderAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail, UserProfile.Customer);

        var resp = await CustomerClient(token).GetAsync(Route(a.OrderId));

        HttpAssert.IsOk(resp);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var body = doc.RootElement;
        Assert.Equal(a.OrderId, body.GetProperty("orderId").GetString());
        // Seeded three days out with nobody assigned: free, full refund of the 1500 the order carries.
        Assert.Equal(0m, body.GetProperty("feeRate").GetDecimal());
        Assert.Equal(0m, body.GetProperty("feeAmount").GetDecimal());
        Assert.Equal(
            body.GetProperty("totalPrice").GetDecimal(),
            body.GetProperty("refundAmount").GetDecimal());
        Assert.Equal("CZK", body.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task Another_customer_is_refused_the_same_way_a_missing_order_is()
    {
        var a = await ArrangeOrderAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OutsiderId, a.OutsiderEmail, UserProfile.Customer);

        var foreign = await CustomerClient(token).GetAsync(Route(a.OrderId));
        var missing = await CustomerClient(token).GetAsync(Route("order-that-does-not-exist"));

        await HttpAssert.RejectedAsync(foreign, BusinessErrorMessage.OrderNotFound);
        await HttpAssert.RejectedAsync(missing, BusinessErrorMessage.OrderNotFound);
        Assert.Equal(missing.StatusCode, foreign.StatusCode);
        Assert.Equal(
            await missing.Content.ReadAsStringAsync(),
            await foreign.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_anonymous_caller_is_rejected()
    {
        var a = await ArrangeOrderAsync();

        var resp = await CustomerClientAnonymous().GetAsync(Route(a.OrderId));

        Assert.NotEqual(System.Net.HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Quoting_does_not_move_the_order()
    {
        var a = await ArrangeOrderAsync();
        var token = TestJwtFactory.Mint(CustomerAudience, a.OwnerId, a.OwnerEmail, UserProfile.Customer);

        HttpAssert.IsOk(await CustomerClient(token).GetAsync(Route(a.OrderId)));
        HttpAssert.IsOk(await CustomerClient(token).GetAsync(Route(a.OrderId)));

        var statusTracks = await QueryAsync(ctx => ctx.Set<OrderStatusTrack>()
            .IgnoreQueryFilters().CountAsync(h => h.OrderId == a.OrderId));
        var order = await QueryAsync(ctx => ctx.Set<Order>()
            .IgnoreQueryFilters().SingleAsync(o => o.Id == a.OrderId));

        Assert.Equal(1, statusTracks);
        Assert.Null(order.CancelledAt);
        Assert.Null(order.CancellationFeeRate);
        Assert.Null(order.CancellationRefundAmount);
    }
}
