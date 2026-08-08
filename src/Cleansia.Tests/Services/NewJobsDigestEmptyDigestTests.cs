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
/// A digest with nothing in it is never sent. Now that the sweep runs on the clock rather than on
/// events, the empty case is the COMMON case — most cleaners have no fresh job most hours — so the
/// guard between "counted zero" and "enqueued a push" is what stands between an hourly cadence and an
/// hourly notification saying nothing. A cleaner who is pushed a zero learns to swipe the channel away,
/// and the digest is the only channel the platform has for reaching them about work.
///
/// <para>Two independent routes reach zero and the tests take one each: an empty board (nothing
/// offerable in the cleaner's work country at all) and a full board every candidate of which clashes
/// with a commitment the cleaner already holds. The second is the interesting one — the count survives
/// the SQL filters and only collapses in the per-order not-busy loop, so a guard placed before that
/// loop would let it through.</para>
///
/// <para>The watermark must not advance on either route: advancing it would consume the freshness of
/// jobs the cleaner was never told about, and the clash route in particular relies on the released-window
/// source to bring those same jobs back later.</para>
///
/// <para>Run against a real <see cref="CleansiaDbContext"/> over SQLite with the real repositories, so
/// the not-busy predicate is the one production runs.</para>
/// </summary>
public sealed class NewJobsDigestEmptyDigestTests : IDisposable
{
    private const string CountryId = "country-digest-empty";
    private const string OtherCountryId = "country-digest-empty-other";
    private const string EmployeeId = "emp-digest-empty";
    private const string UserId = "user-digest-empty";
    private const int SlotMinutes = 120;

    private static readonly DateTime Slot = DateTime.UtcNow.AddDays(4);

    private readonly SqliteConnection _connection;

    public NewJobsDigestEmptyDigestTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_Cleaner_With_No_Job_In_Their_Work_Country_Is_Not_Pushed_An_Empty_Digest()
    {
        await SeedAsync(offerableInWorkCountry: false, withClashingCommitment: false);

        Assert.Empty(await RunSweepAsync());
        Assert.Null(await ReadWatermarkAsync());
    }

    /// <summary>
    /// The control that makes the two zero tests mean something: the same fixture, minus the reason the
    /// count was zero, does produce a digest. Without it both assertions above would also pass against a
    /// sweep that had stopped notifying anyone at all.
    /// </summary>
    [Fact]
    public async Task The_Same_Cleaner_Is_Pushed_When_A_Job_Actually_Survives()
    {
        await SeedAsync(offerableInWorkCountry: true, withClashingCommitment: false);

        Assert.Equal("1", Assert.Single(await RunSweepAsync())["count"]);
        Assert.NotNull(await ReadWatermarkAsync());
    }

    /// <summary>
    /// The candidate passes every SQL filter and dies in the in-memory not-busy loop, so the count is
    /// zero while the query returned a row. This is the route a guard written before the loop misses.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Whose_Only_Candidate_Clashes_Is_Not_Pushed_A_Zero()
    {
        await SeedAsync(offerableInWorkCountry: true, withClashingCommitment: true);

        Assert.Empty(await RunSweepAsync());
        Assert.Null(await ReadWatermarkAsync());
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

    private async Task SeedAsync(bool offerableInWorkCountry, bool withClashingCommitment)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var seed = NewContext();

        var user = User.CreateWithPassword(
            "empty.cleaner@cleansia.test", "Test-password-1!", "Eva", "Empty", UserProfile.Employee);
        user.Id = UserId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));

        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = EmployeeId;
        cleaner.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        cleaner.Approve(approvedByUserId: "admin-digest-empty");
        cleaner.AssignWorkCountry(CountryId);
        seed.Add(cleaner);

        // Always present, so the sweep is never trivially starved of orders: it sits in a country this
        // cleaner is not approved for, which is exactly the "nearby" term the digest actually applies.
        seed.Add(NewOfferableOrder("order-abroad", Slot, OtherCountryId));

        if (offerableInWorkCountry)
        {
            seed.Add(NewOfferableOrder("order-home", Slot, CountryId));
        }

        if (withClashingCommitment)
        {
            var commitment = NewOfferableOrder("order-commitment", Slot, CountryId);
            commitment.AddAssignedEmployee(OrderEmployee.Create(commitment, cleaner));
            seed.Add(commitment);
        }

        await seed.CommitAsync(CancellationToken.None);
    }

    private static Order NewOfferableOrder(string orderId, DateTime cleaningDateTime, string countryId)
    {
        var order = Order.Create(
            customerName: "Empty Customer",
            customerEmail: "empty-customer@cleansia.test",
            customerPhone: "+420777444555",
            customerAddress: Address.Create("Empty St 1", "Praha", "14000", countryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: "czk",
            paymentStatus: PaymentStatus.Paid);
        order.Id = orderId;
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

    private sealed class NullTenantProvider : ITenantProvider
    {
        private string? _tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
