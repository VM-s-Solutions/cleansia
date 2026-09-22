using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Repositories;

public interface IGuestOrderAccessTokenRepository : IRepository<GuestOrderAccessToken, string>
{
    /// <summary>
    /// Every live token of an order, tenant filter ignored: a guest's credential is presented
    /// anonymously, so the request carries no tenant claim to scope by — the unguessable hash is the
    /// scope, exactly as it is for a refresh token.
    /// </summary>
    Task<IReadOnlyList<GuestOrderAccessToken>> GetLiveForOrderIgnoringTenantAsync(
        string orderId, DateTimeOffset now, CancellationToken cancellationToken);
}
