using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The digest's one-read form of the overlap question against the single-window form, over REAL
/// PostgreSQL. The batch reads the cleaner's commitments once and judges each window in memory with
/// <see cref="Order.OccupiesWindow"/>; the single-window form asks the database per window. They are two
/// pieces of text for one rule, so this asks both about the same windows over the same rows and requires
/// the same answer — and a literal one, so they cannot agree on a wrong verdict.
///
/// <para>Each case sits in its own block of days, far enough apart that only the rows of its block can
/// touch it, while every live row of the probe cleaner is still fetched by the batch: a verdict leaking
/// from one case to another would show here.</para>
/// </summary>
[Collection("PostgresCollection")]
public class LiveCommitmentWindowsAgreementTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "cur-czk-lcw";
    private const string CountryId = "ctry-cz-lcw";
    private const string ProbeCleaner = "emp-lcw-probe";
    private const string OtherCleaner = "emp-lcw-other";
    private const int FullSpanPlusHalfHour = (Order.MaxOrderSpanHours * 60) + 30;

    private static readonly DateTime Origin = new(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);

    private static DateTime Block(int index) => Origin.AddDays(index * 20);

    [Fact]
    public async Task The_Batch_Form_And_The_Single_Window_Form_Find_The_Same_Clashes()
    {
        await TestMethod(
            arrange: SeedCommitmentsAsync,
            act: async provider =>
            {
                var repository = provider.GetRequiredService<IOrderRepository>();
                var candidates = Candidates()
                    .Select(c => (c.Id, c.Start, c.Minutes))
                    .ToList();

                var fromBatch = await repository.GetOverlappedCandidateIdsIgnoringTenantAsync(
                    ProbeCleaner, candidates, CancellationToken.None);

                var fromSingle = new List<string>();
                foreach (var (id, start, minutes) in candidates)
                {
                    if (await repository.HasOverlappingOrderIgnoringTenantAsync(
                            ProbeCleaner, start, minutes, CancellationToken.None))
                    {
                        fromSingle.Add(id);
                    }
                }

                return (Batch: fromBatch.Order().ToList(), Single: fromSingle.Order().ToList());
            },
            assert: (CleansiaDbContext _, (List<string> Batch, List<string> Single) result) =>
            {
                var expected = Candidates().Where(c => c.Clashes).Select(c => c.Id).Order().ToList();

                Assert.Equal(expected, result.Single);
                Assert.Equal(result.Single, result.Batch);

                return Task.CompletedTask;
            });
    }

    /// <summary>The windows asked about, each with the verdict written out rather than computed.</summary>
    private static List<Candidate> Candidates()
    {
        var statusCases = Enum.GetValues<OrderStatus>()
            .Select((status, day) => new Candidate(
                $"status-{status}",
                Block(0).AddDays(day).AddMinutes(30),
                60,
                status is not (OrderStatus.Completed or OrderStatus.Cancelled)));

        var commitment = Block(1);
        var later = Block(4).AddHours(100);

        return
        [
            .. statusCases,
            new("touch-start", commitment.AddMinutes(-120), 120, false),
            new("touch-start-short", commitment.AddMinutes(-60), 60, false),
            new("touch-end", commitment.AddMinutes(120), 60, false),
            new("touch-end-long", commitment.AddMinutes(120), 240, false),
            new("overlap-start", commitment.AddMinutes(-119), 120, true),
            new("overlap-end", commitment.AddMinutes(119), 60, true),
            new("inside", commitment.AddMinutes(30), 60, true),
            new("contains", commitment.AddMinutes(-60), 240, true),
            new("floor-edge", Block(2), 60, true),
            new("floor-beyond", Block(3), 60, false),
            new("long-row-earlier", Block(4), 120, true),
            new("long-row-later", later, 120, false),
            new("other-cleaner", Block(5).AddMinutes(30), 60, false),
            new("other-company", Block(6).AddMinutes(30), 60, true),
            new("no-commitment", Block(7), 60, false),
        ];
    }

    private static async Task SeedCommitmentsAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var probe = NewCleaner(ProbeCleaner, "Petra", "Probe");
        var other = NewCleaner(OtherCleaner, "Olga", "Other");
        context.AddRange(probe, other);

        var statusDay = 0;
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            context.Add(NewCommitment($"lcw-status-{status}", Block(0).AddDays(statusDay++), 120, probe, status));
        }

        context.Add(NewCommitment("lcw-boundaries", Block(1), 120, probe));
        context.Add(NewCommitment(
            "lcw-floor-edge", Block(2).AddHours(-Order.MaxOrderSpanHours), FullSpanPlusHalfHour, probe));
        context.Add(NewCommitment(
            "lcw-floor-beyond", Block(3).AddHours(-Order.MaxOrderSpanHours).AddMinutes(-1), FullSpanPlusHalfHour, probe));
        context.Add(NewCommitment(
            "lcw-long-row",
            Block(4).AddHours(100).AddHours(-Order.MaxOrderSpanHours).AddMinutes(-1),
            200 * 60,
            probe));
        context.Add(NewCommitment("lcw-other-cleaner", Block(5), 120, other));

        var otherCompany = NewCommitment("lcw-other-company", Block(6), 120, probe);
        otherCompany.TenantId = TestTenants.Second;
        context.Add(otherCompany);

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewCommitment(
        string orderId,
        DateTime cleaningDateTime,
        int estimatedMinutes,
        Employee assignee,
        OrderStatus status = OrderStatus.Confirmed)
    {
        var order = Order.Create(
            customerName: "Window Customer",
            customerEmail: "window-customer@cleansia.test",
            customerPhone: "+420777111444",
            customerAddress: Address.Create("Window St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: cleaningDateTime,
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.UpdateEstimatedTime(estimatedMinutes);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        if (status != OrderStatus.New)
        {
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
        }

        order.AddAssignedEmployee(OrderEmployee.Create(order, assignee));
        return order;
    }

    private static Employee NewCleaner(string employeeId, string firstName, string lastName)
    {
        var user = User.CreateWithPassword(
            $"{employeeId}@cleansia.test",
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            firstName,
            lastName,
            UserProfile.Employee);
        user.Id = $"user-{employeeId}";
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Approve(approvedByUserId: "admin-lcw");
        employee.AssignWorkCountry(CountryId);
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }

    private sealed record Candidate(string Id, DateTime Start, int Minutes, bool Clashes);
}
