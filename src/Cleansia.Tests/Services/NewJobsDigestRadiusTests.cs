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

    private async Task<DateTimeOffset?> ReadWatermarkAsync()
    {
        await using var ctx = NewContext();
        var employee = await ctx.Set<Employee>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(e => e.Id == EmployeeId);
        return employee.LastNewJobsDigestAt;
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
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid);
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
            new NullTenantProvider());

    private sealed record JobFixture(
        string OrderId, string CountryId, double? Latitude, double? Longitude);

    private sealed class NullTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
