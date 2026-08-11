namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// ADR-0035 D2 / AM-10 — the ONLY place a <c>MembershipBenefitUsage.PeriodKey</c> is ever built.
///
/// <para>The key is <b>order-independent</b> on purpose. Three of the four sites that need it have no
/// address: <c>QuoteOrder.Command</c> carries no country, <c>CreateOrder.Validator</c> runs before the
/// address is resolved, and <c>GetMyMembership.Query</c> is parameterless. An order-anchored key would
/// therefore fall back to UTC on the preview paths while the reservation used the real zone, and every
/// booking made between midnight and 02:00 local on the 1st of a month would count one month's slots and
/// write the other's — deterministically, monthly, forever.</para>
///
/// <para>The zone comes from the platform's <c>CountryConfiguration</c>, never from the client's
/// <c>X-Time-Zone</c> header: the header is unauthenticated, and a member who can pick their own month
/// boundary can draw two months' credits in one evening.</para>
///
/// <para>Never recompute the key for an existing row. It is storage vocabulary and crosses no DTO
/// boundary.</para>
/// </summary>
public interface IBenefitPeriodKeyFactory
{
    /// <summary>
    /// The calendar-month key for <paramref name="nowUtc"/>, e.g. <c>"C:2026-08"</c> — a <c>C:</c>
    /// discriminator plus <c>yyyy-MM</c> of the local wall clock. The discriminator is what lets a future
    /// billing-anchored benefit share this column, this index and this reservation statement with no
    /// migration.
    /// </summary>
    Task<string> GetCalendarKeyAsync(DateTime nowUtc, CancellationToken cancellationToken);
}
