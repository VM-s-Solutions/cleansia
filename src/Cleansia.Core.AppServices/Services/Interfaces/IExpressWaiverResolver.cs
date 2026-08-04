namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// The answer to "does this booking get its express surcharge waived, and how many waivers are left".
///
/// <para><see cref="RemainingBeforeThisBooking"/> has exactly ONE definition everywhere it is read or
/// rendered: live slots left in the current period <b>before</b> the booking under consideration. A
/// caller that wants "after" computes <c>remaining - (waived ? 1 : 0)</c>. Two definitions of one
/// number, differing by one, is an off-by-one in five locales.</para>
///
/// <para><see cref="PeriodKey"/> is internal storage vocabulary and never crosses a DTO boundary.</para>
/// </summary>
public sealed record ExpressWaiver(
    bool InExpressWindow,
    bool Waived,
    int Quota,
    int RemainingBeforeThisBooking,
    string PeriodKey,
    string? UserId = null,
    string? UserMembershipId = null)
{
    public static ExpressWaiver None(string periodKey)
        => new(InExpressWindow: false, Waived: false, Quota: 0,
            RemainingBeforeThisBooking: 0, PeriodKey: periodKey);
}

/// <summary>
/// ADR-0035 D6 — a PURE READ. Never writes, never consumes.
///
/// <para>That is the whole reason the seam exists: <c>CreateOrder.Validator.PriceMatchesAsync</c> re-runs
/// the pricing calculator and <c>QuoteOrder</c> runs it earlier still, so a waived member's price is
/// computed at least three times before the order exists. A resolver that consumed would burn a credit on
/// every quote and on every rejected order. Consumption happens once, at persist, through
/// <see cref="IExpressWaiverConsumer"/>.</para>
/// </summary>
public interface IExpressWaiverResolver
{
    /// <summary>
    /// <paramref name="cleaningUtc"/> <c>null</c> means "no booking under consideration, just report the
    /// balance" — the membership read surface and the pre-slot wizard quote.
    /// <paramref name="nowUtc"/> is captured ONCE per request and threaded: the express window is
    /// [2h, 4h) and lead time shrinks as the request proceeds, so an unthreaded clock lets the resolver
    /// see "in window" and the policy see "out of window" a few statements later — a live slot attached
    /// to an order carrying no surcharge, which is the one loss in this design with no release path.
    /// </summary>
    Task<ExpressWaiver> ResolveForUserAsync(
        string? userId,
        DateTime? cleaningUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
