using System.Linq.Expressions;

namespace Cleansia.Core.Domain.Orders;

/// <summary>
/// Q-FEED-03 — the ONE definition of "is this job near this cleaner". It is a third, independent
/// question beside <see cref="OrderAvailability"/> ("is this live work anyone may take") and
/// <see cref="OrderVisibility"/> ("is it open to this cleaner right now"); surfaces conjoin the ones
/// they need and none re-derives this one.
///
/// <para><b>It is a targeting preference, never an eligibility rule.</b> The service-area model
/// validates the CUSTOMER's address and deliberately lets cleaners live anywhere and commute into
/// served cities; nothing here narrows what a cleaner may see on the board or take. It narrows only
/// what they are PUSHED, which is the sentence the partner app promises ("jobs near you") and the one
/// the platform was not keeping.</para>
///
/// <para><b>Both cleaner-side gaps fail OPEN</b> (see <see cref="Applies"/>): a cleaner with no radius
/// has expressed no preference, and a cleaner whose address never geocoded is carrying a platform
/// failure, not a choice. Neither may have their job feed silently emptied — an absent notification
/// generates no complaint, so a fail-closed default is invisible to exactly the person it harms. The
/// ORDER side fails CLOSED instead (see <see cref="IsWithinRadius"/>): an order of unknown distance is
/// not "near", and counting it re-tells the lie.</para>
///
/// <para><b>Two evaluation stages, not two forms of one rule.</b> <see cref="BoundingBox"/> is a coarse
/// SUPERSET evaluated in SQL as four scalar comparisons on columns the join already fetches, so the
/// sweep does not haul a whole country's board into memory once per cleaner and no trigonometry ends up
/// in a predicate; <see cref="IsWithinRadius"/> is the exact great-circle test, evaluated in memory on
/// the rows the box admitted. The box's only obligation is containment (everything the circle admits,
/// it admits), which is asserted directly rather than assumed; the circle alone decides.</para>
/// </summary>
public static class JobProximity
{
    /// <summary>Mean Earth radius (IUGG), shared by the box and the exact distance so the two agree.</summary>
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>
    /// A hair of slack on the box so a point sitting exactly on the circle cannot be dropped by
    /// double rounding before the exact test ever sees it. ~0.11 m — the box is a prefilter, so
    /// widening it costs nothing and narrowing it loses jobs.
    /// </summary>
    private const double BoxPaddingDegrees = 1e-6;

    public const int MinRadiusKm = 1;

    /// <summary>
    /// Wider than the longest span of any country the platform serves, so the cap never refuses a
    /// meaningful preference; a cleaner who wants everything leaves the radius unset instead.
    /// </summary>
    public const int MaxRadiusKm = 500;

    /// <summary>
    /// Whether the distance filter can be applied at all. Both fallbacks the Q-FEED-03 ruling left open
    /// resolve here, and they resolve the same way: no radius set, or no geocoded home, means no filter
    /// — the cleaner keeps today's country-wide reach.
    /// </summary>
    public static bool Applies(int? radiusKm, double? originLatitude, double? originLongitude)
        => radiusKm is not null && originLatitude is not null && originLongitude is not null;

    /// <summary>Great-circle distance in kilometres (haversine).</summary>
    public static double DistanceKm(
        double originLatitude, double originLongitude, double latitude, double longitude)
    {
        var originLatRad = ToRadians(originLatitude);
        var latRad = ToRadians(latitude);
        var deltaLat = latRad - originLatRad;
        var deltaLon = ToRadians(longitude - originLongitude);

        var h = (Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d))
            + (Math.Cos(originLatRad) * Math.Cos(latRad)
                * Math.Sin(deltaLon / 2d) * Math.Sin(deltaLon / 2d));

        return 2d * EarthRadiusKm * Math.Asin(Math.Min(1d, Math.Sqrt(h)));
    }

    /// <summary>
    /// The exact rule. A null coordinate on the ORDER is not near — see the type remarks for why this
    /// one arm fails closed while the cleaner-side ones fail open.
    /// </summary>
    public static bool IsWithinRadius(
        double originLatitude,
        double originLongitude,
        double? latitude,
        double? longitude,
        int radiusKm)
        => latitude is not null
        && longitude is not null
        && DistanceKm(originLatitude, originLongitude, latitude.Value, longitude.Value) <= radiusKm;

    /// <summary>
    /// The smallest latitude/longitude rectangle containing the whole radius circle. The longitude
    /// half-span is <c>asin(sin δ / cos φ)</c>, not <c>δ / cos φ</c>: the naive form under-covers, and
    /// under-covering a prefilter deletes jobs at the east/west extremes where nobody looks.
    ///
    /// <para>When the circle swallows a pole or wraps the antimeridian there is no such rectangle, so
    /// the longitude term degrades to the whole meridian and the exact test carries the whole rule.</para>
    /// </summary>
    public static GeoBoundingBox BoundingBox(double originLatitude, double originLongitude, int radiusKm)
    {
        var angular = radiusKm / EarthRadiusKm;
        var originLatRad = ToRadians(originLatitude);

        var minLatRad = originLatRad - angular;
        var maxLatRad = originLatRad + angular;

        if (minLatRad <= -Math.PI / 2d || maxLatRad >= Math.PI / 2d)
        {
            return new GeoBoundingBox(
                Math.Max(-90d, ToDegrees(minLatRad)), Math.Min(90d, ToDegrees(maxLatRad)), -180d, 180d);
        }

        var deltaLonSin = Math.Sin(angular) / Math.Cos(originLatRad);
        if (deltaLonSin is > 1d or < -1d)
        {
            return new GeoBoundingBox(ToDegrees(minLatRad), ToDegrees(maxLatRad), -180d, 180d);
        }

        var deltaLon = ToDegrees(Math.Asin(deltaLonSin)) + BoxPaddingDegrees;
        var minLon = originLongitude - deltaLon;
        var maxLon = originLongitude + deltaLon;

        if (minLon < -180d || maxLon > 180d)
        {
            return new GeoBoundingBox(ToDegrees(minLatRad), ToDegrees(maxLatRad), -180d, 180d);
        }

        return new GeoBoundingBox(
            ToDegrees(minLatRad) - BoxPaddingDegrees,
            ToDegrees(maxLatRad) + BoxPaddingDegrees,
            minLon,
            maxLon);
    }

    /// <summary>
    /// Queryable form of the box, over the order's customer address. NULL coordinates fall out of the
    /// comparisons on their own, which is the order-side fail-closed arm — do not add a null-tolerant
    /// disjunct here without changing <see cref="IsWithinRadius"/> in the same edit.
    /// </summary>
    public static Expression<Func<Order, bool>> WithinBoundingBoxSql(GeoBoundingBox box) => order =>
        order.CustomerAddress!.Latitude >= box.MinLatitude
        && order.CustomerAddress.Latitude <= box.MaxLatitude
        && order.CustomerAddress.Longitude >= box.MinLongitude
        && order.CustomerAddress.Longitude <= box.MaxLongitude;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double ToDegrees(double radians) => radians * 180d / Math.PI;
}

public readonly record struct GeoBoundingBox(
    double MinLatitude,
    double MaxLatitude,
    double MinLongitude,
    double MaxLongitude);
