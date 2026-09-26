using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The one place a presented guest credential becomes a booking. The token is resolved by the hash of
/// what the caller sent, against a row that is neither expired nor revoked; a token that matches
/// nothing yields an empty set, so every caller answers "not found" and none of them is an oracle for
/// which bookings exist.
/// </summary>
public sealed class GuestOrderAccess(
    IOrderRepository orderRepository,
    IGuestOrderAccessTokenRepository tokenRepository)
{
    public IQueryable<Order> OrdersForKey(IGuestOrderScopedRequest key) =>
        OrdersForTokens(key.AccessToken is null ? [] : [key.AccessToken]);

    public IQueryable<Order> OrdersForTokens(IReadOnlyCollection<string> accessTokens)
    {
        // Tenant-ignoring on both sides (ADR-0051's bypass-and-re-pin cell): a guest who booked under
        // operator A presents the token without knowing which operator that was, and the hash is the pin.
        var orders = orderRepository.GetQueryableIgnoringTenant();

        var hashes = accessTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(SecurityTokens.Hash)
            .Distinct()
            .ToList();
        if (hashes.Count == 0)
        {
            return orders.Where(o => false);
        }

        var now = DateTimeOffset.UtcNow;
        var orderIds = tokenRepository.GetQueryableIgnoringTenant()
            .Where(t => hashes.Contains(t.TokenHash) && t.RevokedOn == null && t.ExpiresOn > now)
            .Select(t => t.OrderId);

        return orders.Where(o => o.UserId == null && orderIds.Contains(o.Id));
    }
}
