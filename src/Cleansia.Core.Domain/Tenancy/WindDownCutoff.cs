namespace Cleansia.Core.Domain.Tenancy;

/// <summary>
/// The instant a company's last day of service begins in one of its markets (ADR-0064 D2): midnight
/// of <c>WindDownFrom</c> in the market's zone. The settlement reader counts by it and the sweep
/// cancels by it, so both must compute it here and nowhere else.
/// </summary>
public static class WindDownCutoff
{
    public static DateTime Utc(DateOnly windDownFrom, string? timeZoneId)
    {
        var midnight = windDownFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(midnight, ResolveZone(timeZoneId));
    }

    public static DateOnly LocalDate(DateTimeOffset instant, string? timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, ResolveZone(timeZoneId)).DateTime);

    // FindSystemTimeZoneById throws on an unknown id and neither a settlement read nor a sweep may
    // throw over one; UTC is the fallback for a market without a zone.
    private static TimeZoneInfo ResolveZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
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
