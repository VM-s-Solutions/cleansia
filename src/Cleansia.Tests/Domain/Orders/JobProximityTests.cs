using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Domain.Orders;

/// <summary>
/// The proximity rule as pure arithmetic, on real places far enough apart that no assertion here can
/// pass by rounding: Prague → Kladno is 25.2 km, Prague → Ostrava is 275.1 km, and every radius used
/// below sits at least 2× away from both.
///
/// <para>The bounding box is deliberately NOT a second form of the circle — it is a coarse SUPERSET
/// whose only obligation is that it never drops a point the circle admits. That obligation is what the
/// box tests assert, because a box that is merely "about right" silently deletes jobs at the compass
/// points where a circle bulges past a naive rectangle.</para>
/// </summary>
public class JobProximityTests
{
    private const double PragueLat = 50.0755;
    private const double PragueLon = 14.4378;
    private const double KladnoLat = 50.1477;
    private const double KladnoLon = 14.1028;
    private const double OstravaLat = 49.8209;
    private const double OstravaLon = 18.2625;

    [Fact]
    public void Distance_Matches_The_Great_Circle_Between_Two_Real_Places()
    {
        Assert.Equal(25.20, JobProximity.DistanceKm(PragueLat, PragueLon, KladnoLat, KladnoLon), 1);
        Assert.Equal(275.09, JobProximity.DistanceKm(PragueLat, PragueLon, OstravaLat, OstravaLon), 1);
    }

    [Fact]
    public void Distance_Is_Zero_For_A_Point_Against_Itself()
    {
        Assert.Equal(0d, JobProximity.DistanceKm(PragueLat, PragueLon, PragueLat, PragueLon), 6);
    }

    [Fact]
    public void A_Job_Inside_The_Radius_Is_Near()
    {
        Assert.True(JobProximity.IsWithinRadius(PragueLat, PragueLon, KladnoLat, KladnoLon, 50));
    }

    [Fact]
    public void A_Job_Outside_The_Radius_Is_Not_Near()
    {
        Assert.False(JobProximity.IsWithinRadius(PragueLat, PragueLon, OstravaLat, OstravaLon, 50));
    }

    /// <summary>
    /// The order-side fallback the owner's ruling did not name and this build had to decide: an order
    /// whose address never geocoded is NOT "near you" — a count that includes an unknown distance is the
    /// same lie the whole ruling exists to end. It fails CLOSED, unlike both cleaner-side fallbacks.
    /// </summary>
    [Fact]
    public void An_Order_With_No_Coordinates_Is_Never_Near()
    {
        Assert.False(JobProximity.IsWithinRadius(PragueLat, PragueLon, null, KladnoLon, 50));
        Assert.False(JobProximity.IsWithinRadius(PragueLat, PragueLon, KladnoLat, null, 50));
        Assert.False(JobProximity.IsWithinRadius(PragueLat, PragueLon, null, null, 50));
    }

    /// <summary>
    /// The two cleaner-side fallbacks, as one predicate. Both fail OPEN — the filter simply does not
    /// apply — because a filter the platform cannot evaluate must never silently delete a cleaner's
    /// access to work.
    /// </summary>
    [Theory]
    [InlineData(null, PragueLat, PragueLon, false)]
    [InlineData(50, null, PragueLon, false)]
    [InlineData(50, PragueLat, null, false)]
    [InlineData(50, null, null, false)]
    [InlineData(50, PragueLat, PragueLon, true)]
    public void The_Filter_Applies_Only_When_Both_A_Radius_And_An_Origin_Exist(
        int? radiusKm, double? originLatitude, double? originLongitude, bool expected)
    {
        Assert.Equal(expected, JobProximity.Applies(radiusKm, originLatitude, originLongitude));
    }

    /// <summary>
    /// The box's whole contract: everything the circle admits, the box admits too. Walked around the
    /// full compass at the exact radius, which is where a rectangle built from a naive
    /// <c>δ / cos φ</c> longitude span starts clipping — the east/west extremes need
    /// <c>asin(sin δ / cos φ)</c>, the north/south ones do not.
    ///
    /// <para>The largest permitted radius is in the set on purpose: the naive form under-covers by
    /// ~4 m at 50 km, which any sane padding hides, and by ~700 m at 500 km, which nothing does. A
    /// containment test run only at the small radius is green against the wrong formula.</para>
    /// </summary>
    [Theory]
    [InlineData(50)]
    [InlineData(JobProximity.MaxRadiusKm)]
    public void The_Box_Contains_Every_Point_The_Circle_Contains(int radiusKm)
    {
        var box = JobProximity.BoundingBox(PragueLat, PragueLon, radiusKm);

        for (var bearing = 0; bearing < 360; bearing++)
        {
            var (lat, lon) = PointAtBearing(PragueLat, PragueLon, bearing, radiusKm);

            Assert.True(
                lat >= box.MinLatitude && lat <= box.MaxLatitude
                && lon >= box.MinLongitude && lon <= box.MaxLongitude,
                $"bearing {bearing}° at {radiusKm} km fell outside the box: ({lat}, {lon}) vs {box}");
        }
    }

    /// <summary>
    /// And it is a useful superset, not a trivially true one: Ostrava is 275 km away and a box that
    /// spanned the country would pass the containment test above while filtering nothing.
    /// </summary>
    [Fact]
    public void The_Box_Still_Excludes_A_Job_Far_Outside_The_Radius()
    {
        var box = JobProximity.BoundingBox(PragueLat, PragueLon, 50);

        Assert.False(
            OstravaLat >= box.MinLatitude && OstravaLat <= box.MaxLatitude
            && OstravaLon >= box.MinLongitude && OstravaLon <= box.MaxLongitude);
    }

    /// <summary>
    /// A radius that swallows a pole has no finite longitude window, so the box degrades to the whole
    /// meridian rather than emitting a wrapped range no <c>BETWEEN</c> can express. Same for a radius
    /// wide enough to wrap the antimeridian.
    /// </summary>
    [Theory]
    [InlineData(89.5, 14.4378, 200)]
    [InlineData(50.0755, 179.9, 200)]
    [InlineData(50.0755, 14.4378, JobProximity.MaxRadiusKm * 40)]
    public void A_Box_That_Cannot_Be_Expressed_As_A_Range_Degrades_To_The_Whole_Meridian(
        double latitude, double longitude, int radiusKm)
    {
        var box = JobProximity.BoundingBox(latitude, longitude, radiusKm);

        Assert.Equal(-180d, box.MinLongitude);
        Assert.Equal(180d, box.MaxLongitude);
    }

    [Theory]
    [InlineData(0d, HomeDistanceBand.Under3Km)]
    [InlineData(2.999d, HomeDistanceBand.Under3Km)]
    [InlineData(3.0d, HomeDistanceBand.From3To6Km)]
    [InlineData(5.999d, HomeDistanceBand.From3To6Km)]
    [InlineData(6.0d, HomeDistanceBand.From6To10Km)]
    [InlineData(9.999d, HomeDistanceBand.From6To10Km)]
    [InlineData(10.0d, HomeDistanceBand.From10To20Km)]
    [InlineData(19.999d, HomeDistanceBand.From10To20Km)]
    [InlineData(20.0d, HomeDistanceBand.Over20Km)]
    [InlineData(500d, HomeDistanceBand.Over20Km)]
    public void A_Distance_Falls_In_The_Band_Whose_Lower_Edge_It_Reaches(double km, HomeDistanceBand expected)
    {
        Assert.Equal(expected, JobProximity.ClassifyBand(km));
    }

    /// <summary>
    /// No band without both ends: an admin reads no home, a cleaner whose home never geocoded has no
    /// origin, and an order not yet geocoded has no point. Each null arm on its own.
    /// </summary>
    [Theory]
    [InlineData(null, PragueLon, KladnoLat, KladnoLon)]
    [InlineData(PragueLat, null, KladnoLat, KladnoLon)]
    [InlineData(PragueLat, PragueLon, null, KladnoLon)]
    [InlineData(PragueLat, PragueLon, KladnoLat, null)]
    public void A_Missing_Coordinate_On_Either_End_Gives_No_Band(
        double? homeLatitude, double? homeLongitude, double? jobLatitude, double? jobLongitude)
    {
        Assert.Null(JobProximity.Band(homeLatitude, homeLongitude, jobLatitude, jobLongitude));
    }

    [Fact]
    public void Both_Ends_Present_Give_A_Band()
    {
        Assert.Equal(HomeDistanceBand.Over20Km, JobProximity.Band(PragueLat, PragueLon, KladnoLat, KladnoLon));
    }

    /// <summary>
    /// The guard against locating a customer by moving one's home: a cleaner can set any home point, so
    /// a band measured to the exact job point leaks where its edges cross. Two job points inside one cell
    /// must read the same band from every origin, swept in 1 km steps across ±30 km — wide enough to
    /// cross every band edge, which the last assertion checks so the sweep cannot pass vacuously.
    /// </summary>
    [Fact]
    public void Two_Jobs_In_One_Cell_Read_The_Same_Band_From_Every_Home()
    {
        var first = (Latitude: 50.0610, Longitude: 14.4310);
        var second = (Latitude: 50.0790, Longitude: 14.4590);
        Assert.Equal(
            JobProximity.SnapToBandCell(first.Latitude, first.Longitude),
            JobProximity.SnapToBandCell(second.Latitude, second.Longitude));

        var kmPerDegreeLatitude = Math.PI * 6371.0088 / 180d;
        var kmPerDegreeLongitude = kmPerDegreeLatitude * Math.Cos(50.07 * Math.PI / 180d);
        var bandsSeen = new HashSet<HomeDistanceBand>();
        for (var north = -30; north <= 30; north++)
        {
            for (var east = -30; east <= 30; east++)
            {
                var homeLatitude = 50.07 + (north / kmPerDegreeLatitude);
                var homeLongitude = 14.445 + (east / kmPerDegreeLongitude);

                var firstBand = JobProximity.Band(homeLatitude, homeLongitude, first.Latitude, first.Longitude);
                var secondBand = JobProximity.Band(homeLatitude, homeLongitude, second.Latitude, second.Longitude);

                Assert.True(
                    firstBand == secondBand,
                    $"home {north} km N, {east} km E read {firstBand} for one job and {secondBand} for the other");
                bandsSeen.Add(firstBand!.Value);
            }
        }

        Assert.Equal(Enum.GetValues<HomeDistanceBand>().ToHashSet(), bandsSeen);
    }

    /// <summary>
    /// What the snap costs: it moves the point at most half a cell's diagonal (≈1.54 km at Prague's
    /// latitude), so the measured distance never strays from the exact one by more than that. Sampled on
    /// a grid of jobs across Prague from homes in the centre and on three edges of the city.
    /// </summary>
    [Fact]
    public void The_Snapped_Distance_Stays_Within_1_6_Km_Of_The_Exact_One_Around_Prague()
    {
        (double Latitude, double Longitude)[] homes =
        [
            (PragueLat, PragueLon), (KladnoLat, KladnoLon), (49.9700, 14.4700), (50.1400, 14.6200),
        ];

        var worst = 0d;
        for (var latitude = 49.95; latitude <= 50.20; latitude += 0.0037)
        {
            for (var longitude = 14.22; longitude <= 14.70; longitude += 0.0053)
            {
                var (snappedLatitude, snappedLongitude) = JobProximity.SnapToBandCell(latitude, longitude);
                foreach (var home in homes)
                {
                    var exact = JobProximity.DistanceKm(home.Latitude, home.Longitude, latitude, longitude);
                    var snapped = JobProximity.DistanceKm(home.Latitude, home.Longitude, snappedLatitude, snappedLongitude);
                    worst = Math.Max(worst, Math.Abs(snapped - exact));
                }
            }
        }

        Assert.InRange(worst, 0.5d, 1.6d);
    }

    [Theory]
    [InlineData(50.0755, 14.4378)]
    [InlineData(50.0610, 14.4310)]
    [InlineData(49.1951, 16.6068)]
    [InlineData(-33.8688, 151.2093)]
    [InlineData(40.7128, -74.0060)]
    public void Snapping_A_Snapped_Point_Leaves_It_Where_It_Is(double latitude, double longitude)
    {
        var once = JobProximity.SnapToBandCell(latitude, longitude);

        Assert.Equal(once, JobProximity.SnapToBandCell(once.Lat, once.Lon));
    }

    private static (double Latitude, double Longitude) PointAtBearing(
        double latitude, double longitude, double bearingDegrees, double distanceKm)
    {
        const double earthRadiusKm = 6371.0088;

        var angular = distanceKm / earthRadiusKm;
        var bearing = bearingDegrees * Math.PI / 180d;
        var lat = latitude * Math.PI / 180d;
        var lon = longitude * Math.PI / 180d;

        var destLat = Math.Asin(
            (Math.Sin(lat) * Math.Cos(angular)) + (Math.Cos(lat) * Math.Sin(angular) * Math.Cos(bearing)));
        var destLon = lon + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(lat),
            Math.Cos(angular) - (Math.Sin(lat) * Math.Sin(destLat)));

        return (destLat * 180d / Math.PI, destLon * 180d / Math.PI);
    }
}
