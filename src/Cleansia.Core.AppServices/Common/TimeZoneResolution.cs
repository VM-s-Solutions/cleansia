using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Common;

/// <summary>
/// The one place an IANA timezone id becomes a <see cref="TimeZoneInfo"/>.
///
/// <para>Extracted from <c>GetDashboardStats</c> (ADR-0035 AM-10) so the benefit period-key factory can
/// share it: <c>TimeZoneInfo.FindSystemTimeZoneById</c> THROWS on an unknown or malformed id, and a
/// pricing call site must never throw over a time zone.</para>
///
/// <para>Where the id comes from is the caller's decision and the two live callers deliberately differ:
/// the dashboard reads the client's <c>X-Time-Zone</c> header (read-only presentation), the period-key
/// factory reads the platform's <c>CountryConfiguration</c> (an entitlement boundary, so a
/// client-supplied zone would be spoofable).</para>
/// </summary>
public static class TimeZoneResolution
{
    /// <summary>Never throws. Null / blank / unknown / malformed all resolve to UTC.</summary>
    public static TimeZoneInfo Resolve(string? ianaOrWindowsId)
    {
        if (string.IsNullOrWhiteSpace(ianaOrWindowsId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaOrWindowsId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// The clock a booking is read in: the market's configured zone, else the default market's. Server
    /// configuration only — what is bookable must not move with a client's header.
    /// </summary>
    public static async Task<TimeZoneInfo> ForMarketAsync(
        ICountryConfigurationRepository countryConfigurationRepository,
        string? countryId,
        CancellationToken cancellationToken)
    {
        var zoneId = string.IsNullOrEmpty(countryId)
            ? null
            : (await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken))?.TimeZoneId;
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            zoneId = (await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken))?.TimeZoneId;
        }

        return Resolve(zoneId);
    }
}
