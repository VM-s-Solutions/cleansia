using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Mints and retires the guest's booking credential. Stages rows into the caller's unit of work and
/// never commits, so the token and whatever it rides with land in one transaction.
///
/// <para><b>The raw token exists in exactly one place after this call returns: the message the caller
/// is about to send.</b> That is why issuance sits at the e-mail seam rather than at order creation —
/// the confirmation e-mail is composed in another process, from a queue message, and a token minted at
/// creation could only reach it by being written into that message in clear.</para>
/// </summary>
public sealed class GuestOrderAccessTokenIssuer(IGuestOrderAccessTokenRepository repository)
{
    /// <summary>
    /// The raw token to put in the e-mail, or null when the booking belongs to an account and its
    /// owner signs in instead. Any live token of the order is superseded.
    /// </summary>
    public async Task<string?> IssueForGuestAsync(Order order, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(order.UserId))
        {
            return null;
        }

        await RevokeAsync(order, cancellationToken);

        var token = GuestOrderAccessToken.Issue(order.Id, GuestOrderAccessToken.ExpiryFor(order.CleaningDateTime));
        token.TenantId = order.TenantId;
        repository.Add(token);

        return token.RawToken;
    }

    public async Task RevokeAsync(Order order, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var live = await repository.GetLiveForOrderIgnoringTenantAsync(order.Id, now, cancellationToken);
        foreach (var token in live)
        {
            token.Revoke(now);
        }
    }
}
