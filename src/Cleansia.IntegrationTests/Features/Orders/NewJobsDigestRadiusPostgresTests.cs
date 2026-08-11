using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The half of the proximity rule that runs in the DATABASE, against REAL PostgreSQL.
///
/// <para>The digest's bounding box is composed into an EF query, and a predicate that cannot translate
/// throws at runtime while passing every unit test — the SQLite-backed
/// <c>NewJobsDigestRadiusTests</c> included, since the two providers do not agree on what they can
/// translate. So this exercises the production sweep end to end on Postgres and asserts the same
/// verdicts: in-radius reaches the cleaner, out-of-radius does not, and the work-country term still
/// applies on top.</para>
///
/// <para>Real places, 2× clear of every boundary used: home is Prague (50.0755, 14.4378); Kladno is
/// 25.2 km away, Brno 184.3 km, Dresden 119.2 km and in Germany, Ostrava 275.1 km.</para>
/// </summary>
[Collection("PostgresCollection")]
public class NewJobsDigestRadiusPostgresTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-radius";
    private const string CountryId = "country-cz-radius";
    private const string ForeignCountryId = "country-de-radius";
    private const string EmployeeId = "employee-radius-1";
    private const string UserId = "user-radius-1";
    private const string EmployeeEmail = "cleaner-radius@cleansia.test";

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

    [Fact]
    public async Task The_Box_Predicate_Translates_And_Keeps_A_Nearby_Job() =>
        await RunAsync(
            radiusKm: 50,
            expectedCount: "1",
            new Job("order-pg-kladno", CountryId, KladnoLat, KladnoLon));

    [Fact]
    public async Task A_Job_Outside_The_Radius_Is_Filtered_By_PostgreSQL() =>
        await RunAsync(
            radiusKm: 50,
            expectedCount: null,
            new Job("order-pg-ostrava", CountryId, OstravaLat, OstravaLon));

    /// <summary>
    /// Both rows are inside a 200 km circle; only the one in the cleaner's work country counts. A
    /// predicate that replaced the country term with the distance term returns two.
    /// </summary>
    [Fact]
    public async Task The_Work_Country_Term_Survives_Alongside_The_Box() =>
        await RunAsync(
            radiusKm: 200,
            expectedCount: "1",
            new Job("order-pg-brno", CountryId, BrnoLat, BrnoLon),
            new Job("order-pg-dresden", ForeignCountryId, DresdenLat, DresdenLon));

    /// <summary>
    /// The order-side fail-closed arm over the provider whose three-valued logic could break it. Both
    /// stages refuse a NULL coordinate — SQL because a comparison against NULL is not true, C# because
    /// the exact test guards — so this pins the composite verdict, not either stage alone.
    /// </summary>
    [Fact]
    public async Task An_Order_With_NULL_Coordinates_Is_Not_Counted_As_Near_On_PostgreSQL() =>
        await RunAsync(
            radiusKm: 50,
            expectedCount: null,
            new Job("order-pg-null-coords", CountryId, null, null));

    /// <summary>The cleaner-side fail-open arm, proved over the same provider.</summary>
    [Fact]
    public async Task A_Cleaner_With_No_Radius_Still_Gets_The_Whole_Work_Country() =>
        await RunAsync(
            radiusKm: null,
            expectedCount: "1",
            new Job("order-pg-ostrava", CountryId, OstravaLat, OstravaLon));

    private Task RunAsync(int? radiusKm, string? expectedCount, params Job[] jobs) =>
        TestMethod<IReadOnlyList<Dictionary<string, string>>>(
            arrange: context => SeedAsync(context, radiusKm, jobs),
            act: SweepAsync,
            assert: (CleansiaDbContext _, IReadOnlyList<Dictionary<string, string>> pushes) =>
            {
                if (expectedCount is null)
                {
                    Assert.Empty(pushes);
                }
                else
                {
                    Assert.Equal(expectedCount, Assert.Single(pushes)["count"]);
                }

                return Task.CompletedTask;
            });

    private static async Task<IReadOnlyList<Dictionary<string, string>>> SweepAsync(IServiceProvider provider)
    {
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
            provider.GetRequiredService<IEmployeeRepository>(),
            provider.GetRequiredService<IOrderRepository>(),
            provider.GetRequiredService<IUserNotificationPreferencesRepository>(),
            producer.Object,
            provider.GetRequiredService<IUnitOfWork>(),
            NullLogger<NewJobsDigestService>.Instance);

        await digest.SendDigestsAsync(CancellationToken.None);

        return pushes;
    }

    private static async Task SeedAsync(CleansiaDbContext context, int? radiusKm, Job[] jobs)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZE", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var foreignCountry = Country.Create("Germany", "DEU", isServiced: true);
        foreignCountry.Id = ForeignCountryId;
        context.Countries.Add(foreignCountry);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var user = User.CreateWithPassword(
            EmployeeEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Rada",
            "Radius",
            UserProfile.Employee);
        user.Id = UserId;
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = EmployeeId;
        cleaner.Approve(approvedByUserId: "admin-radius");
        cleaner.AssignWorkCountry(CountryId);
        cleaner.SetJobRadius(radiusKm);
        cleaner.UpdateAddress(Address.Create(
            "Home St 1", "Praha", "11000", CountryId, null, PragueLat, PragueLon));
        cleaner.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        context.Add(cleaner);

        var slot = 0;
        foreach (var job in jobs)
        {
            context.Add(NewOfferableOrder(job, Slot.AddHours(slot * 8)));
            slot++;
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOfferableOrder(Job job, DateTime cleaningDateTime)
    {
        var order = Order.Create(
            customerName: "Radius Customer",
            customerEmail: "radius-customer@cleansia.test",
            customerPhone: "+420777444557",
            customerAddress: Address.Create(
                "Job St 1", "Kladno", "27201", job.CountryId, null, job.Latitude, job.Longitude),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1200m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = job.OrderId;
        order.UpdateEstimatedTime(120);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }

    private sealed record Job(string OrderId, string CountryId, double? Latitude, double? Longitude);
}
