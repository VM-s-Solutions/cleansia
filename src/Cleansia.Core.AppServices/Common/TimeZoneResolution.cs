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
}
