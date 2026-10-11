using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// Q-FEED-03 — the digest's "near you" is a per-cleaner radius from their home address, and this is the
/// sweep-level proof that it is applied, that it is applied ON TOP of the work-country term rather than
/// instead of it, and that each of the three fallbacks resolves the way the ruling's follow-up decided.
///
/// <para>Real places, chosen so no assertion can pass by rounding: the cleaner's home is Prague
/// (50.0755, 14.4378); Kladno is 25.2 km away, Brno 184.3 km, Dresden 119.2 km (and in Germany),
/// Ostrava 275.1 km. Every radius used sits at least 2× clear of the nearest boundary.</para>
///
/// <para>Run against a real <see cref="CleansiaDbContext"/> over SQLite with the real repositories, so
/// the board query is the one production issues. The database-side half of the predicate is proved to
/// TRANSLATE against real PostgreSQL by <c>NewJobsDigestRadiusPostgresTests</c>; a bounding box that
/// cannot translate throws at runtime and passes every test in this file.</para>
/// </summary>
public sealed class NewJobsDigestRadiusTests : IDisposable
{
    private const string CountryId = "country-digest-radius-cz";
    private const string ForeignCountryId = "country-digest-radius-de";
    private const string EmployeeId = "emp-digest-radius";
    private const string UserId = "user-digest-radius";
    private const int SlotMinutes = 120;

    private const double PragueLat = 50.0755;
    private const double PragueLon = 14.4378;
    private const double KladnoLat = 50.1477;
    private const double KladnoLon = 14.1028;
    private const double BrnoLat = 49.1951;
    private const double BrnoLon = 16.6068;
    private const double DresdenLat = 51.0504;
    private const double DresdenLon = 13.7373;
    private const double OstravaLat = 49.8209;
    private const double OstravaLon = 18.2625;

    // Synthetic points between a radius's box and its circle: 12.0 km from home, inside the 10 km box;
    // 61.8 km from home, inside the 50 km box.
    private const double Corner10Lat = 50.15;
    private const double Corner10Lon = 14.56;
    private const double Corner50Lat = 50.47;
    private const double Corner50Lon = 15.05;

    private static readonly DateTime Slot = DateTime.UtcNow.AddDays(4);

    private readonly SqliteConnection _connection;

    public NewJobsDigestRadiusTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_Job_Inside_The_Radius_Reaches_The_Cleaner()
    {
        await SeedAsync(
            radiusKm: 50,
            home: (PragueLat, PragueLon),
            new JobFixture("order-kladno", CountryId, KladnoLat, KladnoLon));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
        Assert.NotNull(await ReadWatermarkAsync());
    }

    /// <summary>
    /// The other half, deliberately its own fixture: a single out-of-radius job, so the cleaner's whole
    /// filtered board is empty and the sweep must send NOTHING — not a digest saying zero — and must
    /// leave the watermark unmoved so the job returns if the cleaner widens their radius.
    /// </summary>
    [Fact]
    public async Task A_Job_Outside_The_Radius_Does_Not_And_No_Empty_Digest_Is_Sent()
    {
        await SeedAsync(
            radiusKm: 50,
            home: (PragueLat, PragueLon),
            new JobFixture("order-ostrava", CountryId, OstravaLat, OstravaLon));

        Assert.Empty(await RunSweepAsync());
        Assert.Null(await ReadWatermarkAsync());
    }

    /// <summary>
    /// Distance narrows the work-country board; it does not replace it. Dresden is 119 km from the
    /// cleaner's home — comfortably inside a 200 km radius — and in a country they are not approved to
    /// work in. Brno is 184 km and in it. A sweep that swapped the country term for the distance term
    /// counts two.
    /// </summary>
    [Fact]
    public async Task The_Work_Country_Filter_Still_Applies_On_Top_Of_Distance()
    {
        await SeedAsync(
            radiusKm: 200,
            home: (PragueLat, PragueLon),
            new JobFixture("order-brno", CountryId, BrnoLat, BrnoLon),
            new JobFixture("order-dresden", ForeignCountryId, DresdenLat, DresdenLon));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
    }

    /// <summary>
    /// FALLBACK 1 (cleaner set no radius) — fails OPEN. Ostrava is 275 km away and still reaches them,
    /// because a null column expresses no preference and inventing one silently deletes the only channel
    /// the platform has for telling a cleaner about work.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Who_Has_Set_No_Radius_Keeps_The_Country_Wide_Board()
    {
        await SeedAsync(
            radiusKm: null,
            home: (PragueLat, PragueLon),
            new JobFixture("order-ostrava", CountryId, OstravaLat, OstravaLon));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
    }

    /// <summary>
    /// FALLBACK 2 (the cleaner's home never geocoded) — also fails OPEN, and the radius IS set here, so
    /// this cannot pass by accident through fallback 1. Geocoding is best-effort by construction, so
    /// this is a common path and not a corner case; punishing a cleaner for the platform's own missing
    /// coordinate is the one outcome that has no defence.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Whose_Home_Never_Geocoded_Keeps_The_Country_Wide_Board()
    {
        await SeedAsync(
            radiusKm: 50,
            home: null,
            new JobFixture("order-ostrava", CountryId, OstravaLat, OstravaLon));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
    }

    /// <summary>
    /// The same fallback reached the other way — a cleaner with no address row at all, which is every
    /// cleaner mid-onboarding. It is a separate test because it is a separate code path: the candidate
    /// projection reads through a null navigation, and a projection that threw there would take the
    /// whole sweep down for every cleaner, not just this one.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_With_No_Address_At_All_Keeps_The_Country_Wide_Board()
    {
        await SeedAsync(
            radiusKm: 50,
            home: null,
            withAddress: false,
            new JobFixture("order-ostrava", CountryId, OstravaLat, OstravaLon));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
    }

    /// <summary>
    /// FALLBACK 3, the one the ruling did not name — an ORDER whose address never geocoded. This arm
    /// fails CLOSED: the job sits 25 km away in truth, but the platform cannot know that, and a count
    /// that includes an unknown distance re-tells exactly the lie this build exists to end. The job is
    /// still on the board; only the "near you" claim is withheld.
    /// </summary>
    [Fact]
    public async Task An_Order_Whose_Address_Never_Geocoded_Is_Not_Counted_As_Near()
    {
        await SeedAsync(
            radiusKm: 50,
            home: (PragueLat, PragueLon),
            new JobFixture("order-uncoded", CountryId, null, null));

        Assert.Empty(await RunSweepAsync());
        Assert.Null(await ReadWatermarkAsync());
    }

    /// <summary>
    /// The control for the test above: the SAME address text with coordinates does reach them, so the
    /// refusal is the missing coordinate and not the fixture.
    /// </summary>
    [Fact]
    public async Task The_Same_Order_With_Coordinates_Does_Reach_Them()
    {
        await SeedAsync(
            radiusKm: 50,
            home: (PragueLat, PragueLon),
            new JobFixture("order-uncoded", CountryId, KladnoLat, KladnoLon));

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
    }

    /// <summary>
    /// Only jobs inside the circle reach the commitment read, and the read is per cleaner. The cleaner with
    /// no radius and the 50 km one each cost one read naming their own survivors; the 10 km cleaner's only
    /// candidate passes the box and fails the circle, so they cost no read, get no push and keep no
    /// watermark.
    /// </summary>
    [Fact]
    public async Task Only_Jobs_Inside_The_Circle_Reach_The_Commitment_Read_At_Most_Once_Per_Cleaner()
    {
        AssertInsideTheBoxButOutsideTheCircle(Corner10Lat, Corner10Lon, radiusKm: 10);
        AssertInsideTheBoxButOutsideTheCircle(Corner50Lat, Corner50Lon, radiusKm: 50);

        await SeedCleanersAsync(
            [
                new CleanerFixture("emp-radius-anywhere", "user-radius-anywhere", RadiusKm: null),
                new CleanerFixture("emp-radius-50", "user-radius-50", RadiusKm: 50),
                new CleanerFixture("emp-radius-10", "user-radius-10", RadiusKm: 10),
            ],
            new JobFixture("order-kladno", CountryId, KladnoLat, KladnoLon),
            new JobFixture("order-corner-10", CountryId, Corner10Lat, Corner10Lon),
            new JobFixture("order-corner-50", CountryId, Corner50Lat, Corner50Lon));

        var sweep = await RunCountedSweepAsync();

        Assert.Equal("3", sweep.CountsByUser["user-radius-anywhere"]);
        Assert.Equal("2", sweep.CountsByUser["user-radius-50"]);
        Assert.False(sweep.CountsByUser.ContainsKey("user-radius-10"));
        Assert.Null(await ReadWatermarkAsync("emp-radius-10"));

        Assert.Equal(0, sweep.SingleWindowProbes);
        Assert.Equal(2, sweep.Reads.Count);
        Assert.Equal(
            new[] { "order-corner-10", "order-corner-50", "order-kladno" },
            Assert.Single(sweep.Reads, r => r.EmployeeId == "emp-radius-anywhere").CandidateIds.Order());
        Assert.Equal(
            new[] { "order-corner-10", "order-kladno" },
            Assert.Single(sweep.Reads, r => r.EmployeeId == "emp-radius-50").CandidateIds.Order());
    }

    private async Task<IReadOnlyList<Dictionary<string, string>>> RunSweepAsync()
    {
        await using var ctx = NewContext();

        var pushes = new List<Dictionary<string, string>>();
        var producer = new Mock<INotificationProducer>();
        producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (_, _, args, _, _, _) => pushes.Add(args))
            .Returns(Task.CompletedTask);

        var digest = new NewJobsDigestService(
            new EmployeeRepository(ctx),
            new OrderRepository(ctx),
            new UserNotificationPreferencesRepository(ctx),
            producer.Object,
            ctx,
            NullLogger<NewJobsDigestService>.Instance);

        await digest.SendDigestsAsync(CancellationToken.None);

        return pushes;
    }

    /// <summary>The premise of a corner point: the SQL box keeps it and the exact test drops it.</summary>
    private static void AssertInsideTheBoxButOutsideTheCircle(double latitude, double longitude, int radiusKm)
    {
        var box = JobProximity.BoundingBox(PragueLat, PragueLon, radiusKm);
        Assert.InRange(latitude, box.MinLatitude, box.MaxLatitude);
        Assert.InRange(longitude, box.MinLongitude, box.MaxLongitude);
        Assert.False(JobProximity.IsWithinRadius(PragueLat, PragueLon, latitude, longitude, radiusKm));
    }

    /// <summary>
    /// The sweep with the commitment read counted per cleaner. A lookup with no candidates issues no query
    /// (pinned in <c>HasOverlappingOrderStatusTests</c>), so only the others count as reads; and because
    /// the sweep swallows a cleaner's failure, any swallowed failure fails the test instead of reading as
    /// "nothing to send".
    /// </summary>
    private async Task<CountedSweep> RunCountedSweepAsync()
    {
        await using var ctx = NewContext();

        var counts = new Dictionary<string, string>();
        var producer = new Mock<INotificationProducer>();
        producer
            .Setup(p => p.NotifyAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, Dictionary<string, string>, string?, string?, CancellationToken>(
                (userId, _, args, _, _, _) => counts.Add(userId, args["count"]))
            .Returns(Task.CompletedTask);

        // Strict, so a member nobody set up throws instead of answering null. The single-window probe is
        // still forwarded and counted, so probing per candidate shows up as a count, not as a failure.
        var singleWindowProbes = 0;
        var lookups = new List<(string EmployeeId, IReadOnlyList<string> CandidateIds)>();
        var realOrderRepository = new OrderRepository(ctx);
        var orderRepository = new Mock<IOrderRepository>(MockBehavior.Strict);
        orderRepository
            .Setup(r => r.GetQueryableIgnoringTenant())
            .Returns(() => realOrderRepository.GetQueryableIgnoringTenant());
        orderRepository
            .Setup(r => r.HasOverlappingOrderIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<string, DateTime, int, CancellationToken>((employeeId, start, minutes, ct) =>
            {
                singleWindowProbes++;
                return realOrderRepository.HasOverlappingOrderIgnoringTenantAsync(employeeId, start, minutes, ct);
            });
        orderRepository
            .Setup(r => r.GetOverlappedCandidateIdsIgnoringTenantAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<(string Id, DateTime CleaningDateTime, int EstimatedTimeMinutes)>>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, IReadOnlyCollection<(string Id, DateTime CleaningDateTime, int EstimatedTimeMinutes)>, CancellationToken>(
                (employeeId, candidates, ct) =>
                {
                    lookups.Add((employeeId, [.. candidates.Select(c => c.Id)]));
                    return realOrderRepository.GetOverlappedCandidateIdsIgnoringTenantAsync(employeeId, candidates, ct);
                });

        var swallowed = new List<Exception>();
        var digest = new NewJobsDigestService(
            new EmployeeRepository(ctx),
            orderRepository.Object,
            new UserNotificationPreferencesRepository(ctx),
            producer.Object,
            ctx,
            new FailureCapturingLogger(swallowed));

        await digest.SendDigestsAsync(CancellationToken.None);

        Assert.True(swallowed.Count == 0, $"the sweep swallowed: {string.Join(" | ", swallowed)}");
        return new CountedSweep(counts, singleWindowProbes, [.. lookups.Where(l => l.CandidateIds.Count > 0)]);
    }

    private async Task<DateTimeOffset?> ReadWatermarkAsync(string employeeId = EmployeeId)
    {
        await using var ctx = NewContext();
        var employee = await ctx.Set<Employee>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(e => e.Id == employeeId);
        return employee.LastNewJobsDigestAt;
    }

    /// <summary>Several cleaners, all living in Prague and approved for the same country, so they share one board.</summary>
    private async Task SeedCleanersAsync(IReadOnlyList<CleanerFixture> cleaners, params JobFixture[] jobs)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext();

        foreach (var fixture in cleaners)
        {
            var user = User.CreateWithPassword(
                $"{fixture.EmployeeId}@cleansia.test", "Test-password-1!", "Rada", "Radius", UserProfile.Employee);
            user.Id = fixture.UserId;
            user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

            var cleaner = Employee.CreateWithUser(user);
            cleaner.Id = fixture.EmployeeId;
            cleaner.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
            cleaner.Approve(approvedByUserId: "admin-digest-radius");
            cleaner.AssignWorkCountry(CountryId);
            cleaner.SetJobRadius(fixture.RadiusKm);
            cleaner.UpdateAddress(Address.Create(
                "Home St 1", "Praha", "11000", CountryId, null, PragueLat, PragueLon));
            seed.Add(cleaner);
        }

        var slot = 0;
        foreach (var job in jobs)
        {
            seed.Add(NewOfferableOrder(job, Slot.AddHours(slot * 8)));
            slot++;
        }

        await seed.CommitAsync(CancellationToken.None);
    }

    private async Task SeedAsync(
        int? radiusKm,
        (double Latitude, double Longitude)? home,
        params JobFixture[] jobs) => await SeedAsync(radiusKm, home, withAddress: true, jobs);

    private async Task SeedAsync(
        int? radiusKm,
        (double Latitude, double Longitude)? home,
        bool withAddress,
        params JobFixture[] jobs)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext();

        var user = User.CreateWithPassword(
            "radius.cleaner@cleansia.test", "Test-password-1!", "Rada", "Radius", UserProfile.Employee);
        user.Id = UserId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = EmployeeId;
        cleaner.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        cleaner.Approve(approvedByUserId: "admin-digest-radius");
        cleaner.AssignWorkCountry(CountryId);
        cleaner.SetJobRadius(radiusKm);
        if (withAddress)
        {
            cleaner.UpdateAddress(Address.Create(
                "Home St 1", "Praha", "11000", CountryId, null, home?.Latitude, home?.Longitude));
        }

        seed.Add(cleaner);

        var slot = 0;
        foreach (var job in jobs)
        {
            seed.Add(NewOfferableOrder(job, Slot.AddHours(slot * 8)));
            slot++;
        }

        await seed.CommitAsync(CancellationToken.None);
    }

    private static Order NewOfferableOrder(JobFixture job, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Radius Customer",
            customerEmail: "radius-customer@cleansia.test",
            customerPhone: "+420777444556",
            customerAddress: Address.Create(
                "Job St 1", "Kladno", "27201", job.CountryId, null, job.Latitude, job.Longitude),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = job.OrderId;
        order.UpdateEstimatedTime(SlotMinutes);
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-1));
        AppendTrack(order, OrderStatus.New, DateTimeOffset.UtcNow.AddMinutes(-10));
        AppendTrack(order, OrderStatus.Confirmed, DateTimeOffset.UtcNow.AddMinutes(-5));
        return order;
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new DefaultTenantProvider());

    private sealed record JobFixture(
        string OrderId, string CountryId, double? Latitude, double? Longitude);

    private sealed record CleanerFixture(string EmployeeId, string UserId, int? RadiusKm);

    private sealed record CountedSweep(
        IReadOnlyDictionary<string, string> CountsByUser,
        int SingleWindowProbes,
        IReadOnlyList<(string EmployeeId, IReadOnlyList<string> CandidateIds)> Reads);

    private sealed class FailureCapturingLogger(List<Exception> swallowed) : ILogger<NewJobsDigestService>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null)
            {
                swallowed.Add(exception);
            }
        }
    }

    private sealed class DefaultTenantProvider : ITenantProvider
    {
        private string? _tenantId = TestTenants.Default;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
