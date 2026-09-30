using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// An express booking starts two to four hours out, and every start has to fall inside the bookable day
/// of the market's clock. From a run at night there is no such start in any one fixed zone, so the slot
/// comes with a real zone in which it is daytime, and the test gives the booking's market that zone.
/// </summary>
internal static class ExpressBookingSlot
{
    private static readonly string[] Zones =
    [
        "Europe/Prague", "Asia/Dubai", "Asia/Kolkata", "Asia/Tokyo", "Pacific/Auckland",
        "America/Los_Angeles", "America/New_York", "America/Sao_Paulo",
    ];

    public static (DateTime CleaningUtc, string TimeZoneId) Next()
    {
        var grid = TimeSpan.FromMinutes(BookingPolicy.SlotGridMinutes).Ticks;
        var earliest = DateTime.UtcNow.AddHours(BookingPolicy.ExpressLeadTimeHours).AddMinutes(30);
        var start = new DateTime(earliest.Ticks - earliest.Ticks % grid + grid, DateTimeKind.Utc);

        foreach (var zoneId in Zones)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(start, TimeZoneResolution.Resolve(zoneId));
            if (BookingPolicy.IsBookableTimeOfDay(TimeOnly.FromDateTime(local)))
            {
                return (start, zoneId);
            }
        }

        throw new InvalidOperationException($"No listed zone has {start:O} inside the bookable day.");
    }

    public static async Task PutMarketInZoneAsync(CleansiaDbContext context, string countryId, string timeZoneId)
    {
        var configuration = await context.CountryConfigurations.SingleAsync(c => c.CountryId == countryId);
        context.Entry(configuration).Property(nameof(CountryConfiguration.TimeZoneId)).CurrentValue = timeZoneId;
        await context.CommitAsync(CancellationToken.None);
    }
}
