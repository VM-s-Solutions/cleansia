using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// Mints and retires the guest's booking credential. Stages rows into the caller's unit of work and
/// never commits, so the token and whatever it rides with land in one transaction.
///
/// <para><b>Every issue mints an INDEPENDENT credential and supersedes nothing</b>, so a booking
/// accumulates one row per message that had to carry a working link: the checkout response, the
/// confirmation e-mail, and each status e-mail after it. Superseding is what a single-channel design
/// needs; this is not one, and the cost of it was two dead ends — a guest who opened "your cleaner is
/// on the way" landed on the lookup form, and the checkout success page could not show the booking it
/// had just taken payment for. N live tokens are no weaker than one: each is 256 bits, resolved by its
/// own hash, and scoped to the one booking it was minted for.</para>
///
/// <para>The raw value exists only in the message the caller is about to send, which is why the caller
/// stages and commits BEFORE sending. <see cref="RevokeAsync"/> is the one place the set shrinks, and
/// it empties it: a cancelled booking retires every key that was outstanding.</para>
/// </summary>
public sealed class GuestOrderAccessTokenIssuer(IGuestOrderAccessTokenRepository repository)
{
    /// <summary>
    /// The raw token to put in the message, or null when the booking belongs to an account and its
    /// owner signs in instead.
    /// </summary>
    public string? IssueForGuest(Order order)
    {
        if (!string.IsNullOrEmpty(order.UserId))
        {
            return null;
        }

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
