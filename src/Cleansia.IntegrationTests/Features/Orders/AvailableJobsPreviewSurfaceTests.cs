using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// The dashboard's available-work preview was the THIRD surface serving the population the
/// pre-acceptance redaction exists for — <c>DashboardSpecifications.CreateAvailableOrdersSpec</c> passes
/// <c>excludeEmployeeId</c>, so every row is by construction an order the caller is not on — and it was
/// the one the redaction pass missed. It joined <c>Street, City, ZipCode</c> onto the DTO, and its
/// caller-supplied <c>Limit</c> had no ceiling, so one request returned the street address of the whole
/// available board.
///
/// <para>Over real Postgres because both halves are SQL: the projection is what keeps the street inside
/// the database, and the clamp is a <c>LIMIT</c>.</para>
/// </summary>
[Collection("PostgresCollection")]
public class AvailableJobsPreviewSurfaceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-preview";
    private const string CountryId = "country-cz-preview";
    private const string CallerEmployeeId = "employee-preview-caller";
    private const string CallerUserId = "user-preview-caller";
    private const string CallerEmail = "cleaner-preview@cleansia.test";

    private const string Street = "Vinohradska 12";
    private const string City = "Praha";
    private const string ZipCode = "12000";
    private const string ApproximateAddress = "Praha · 120 xx";

    /// <summary>
    /// One more than the server ceiling, written as a literal rather than read off the constant under
    /// test: an expectation derived from the code it guards cannot detect that code being widened.
    /// </summary>
    private const string PayableCategoryId = "category-preview-payable";
    private const string PayableServiceId = "service-preview-payable";

    /// <summary>
    /// Deliberately not a round fraction of the seeded price (1000 + index): a regression that summed
    /// price, or price times a tidy multiplier, cannot coincide with this.
    /// </summary>
    private const decimal PayPerJob = 337m;

    private const int SeededOrders = 51;

    private const int ServerCeiling = 50;

    [Fact]
    public async Task The_Preview_Serves_The_Coarse_Location_And_Never_The_Street()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTheAvailableBoard,
            act: async provider => await provider
                .GetRequiredService<IMediator>()
                .Send(new GetAvailableJobsPreview.Query(Limit: 5)),
            assert: (CleansiaDbContext _, BusinessResult<AvailableJobsPreviewResponse> result) =>
            {
                Assert.True(result.IsSuccess);
                var response = result.Value!;

                Assert.Equal(5, response.Jobs.Count);
                Assert.All(response.Jobs, job => Assert.Equal(ApproximateAddress, job.CustomerAddressApproximate));

                // Over the SERIALIZED response, not over the location field: the seeded street is real
                // and non-empty, and asserting that a coarse string does not contain it is true by
                // construction. This reddens for any member that carries the street, whatever it is
                // called — which is how the field slipped through the first time.
                Assert.DoesNotContain(Street, JsonSerializer.Serialize(response));

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The clamp, asked for from the outside: the endpoint takes <c>Limit</c> from the query string, so
    /// before this the caller chose the page size and <c>?limit=100000</c> was one request for the whole
    /// board. <c>TotalAvailableCount</c> is deliberately NOT clamped — it is a count, not a disclosure,
    /// and the headline the clients render needs it exact.
    /// </summary>
    [Fact]
    public async Task A_Caller_Cannot_Ask_For_More_Than_The_Server_Ceiling()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTheAvailableBoard,
            act: async provider => await provider
                .GetRequiredService<IMediator>()
                .Send(new GetAvailableJobsPreview.Query(Limit: 100_000)),
            assert: (CleansiaDbContext _, BusinessResult<AvailableJobsPreviewResponse> result) =>
            {
                Assert.True(result.IsSuccess);

                Assert.Equal(ServerCeiling, result.Value!.Jobs.Count);
                Assert.Equal(SeededOrders, result.Value.TotalAvailableCount);

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// The lower end of the same clamp. EF hands <c>Take</c> straight to <c>LIMIT</c>, and PostgreSQL
    /// rejects a negative one — so an unclamped floor is a 500 any caller can trigger from the query
    /// string.
    /// </summary>
    [Fact]
    public async Task A_Negative_Limit_Is_Answered_Rather_Than_Thrown()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTheAvailableBoard,
            act: async provider => await provider
                .GetRequiredService<IMediator>()
                .Send(new GetAvailableJobsPreview.Query(Limit: -1)),
            assert: (CleansiaDbContext _, BusinessResult<AvailableJobsPreviewResponse> result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.Single(result.Value!.Jobs);
                Assert.Equal(SeededOrders, result.Value.TotalAvailableCount);

                return Task.CompletedTask;
            });
    }

    private static Task ReplaceWithCallerSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            CallerUserId,
            CallerEmail,
            [
                new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()),
                new Claim(TestUserSessionProvider.EmployeeIdClaimType, CallerEmployeeId),
            ])));
        return Task.CompletedTask;
    }

    /// <summary>
    /// <b>The banner quotes what the CLEANER earns, never what the customer pays.</b>
    ///
    /// <para>It used to sum <c>Order.TotalPrice</c> under the label "Earn up to X". On a real board that
    /// read 3 731 Kč on the dashboard for a job the orders list offered at 1 275 Kč — the platform
    /// advertising roughly three times the money it would actually pay. The two figures now come from
    /// one definition: <c>OrderPayEstimator</c>, the same estimator the list uses.</para>
    ///
    /// <para>The seeded pay is deliberately NOT a round fraction of the price, so a regression that
    /// reverted to price, or applied a multiplier to it, cannot land on the expected number by
    /// coincidence.</para>
    /// </summary>
    [Fact]
    public async Task The_Headline_Sums_Cleaner_Pay_And_Not_The_Customer_Price()
    {
        await TestMethod(
            setup: ReplaceWithCallerSession,
            arrange: SeedTwoPayableJobs,
            act: async provider => await provider
                .GetRequiredService<IMediator>()
                .Send(new GetAvailableJobsPreview.Query(Limit: 5)),
            assert: (CleansiaDbContext _, BusinessResult<AvailableJobsPreviewResponse> result) =>
            {
                Assert.True(result.IsSuccess);
                var response = result.Value!;

                Assert.Equal(2, response.Jobs.Count);

                // Two jobs, PayPerJob each. The prices are far larger and are what the old code summed.
                Assert.Equal(PayPerJob * 2, response.TotalPotentialEarnings);

                // Stated as its own assertion rather than left implicit: this is the exact number the
                // defect produced, and naming it is what stops a future "simplification" reinstating it.
                var customerPriceSum = response.Jobs.Sum(j => j.TotalPrice);
                Assert.NotEqual(customerPriceSum, response.TotalPotentialEarnings);
                Assert.True(
                    response.TotalPotentialEarnings < customerPriceSum,
                    $"a cleaner cannot earn more than the customer paid: {response.TotalPotentialEarnings} vs {customerPriceSum}");

                return Task.CompletedTask;
            });
    }

    /// <summary>
    /// A board whose orders can actually be priced FOR A CLEANER — the other fixture seeds no services,
    /// so the estimator has no config to match and every row is unquotable. That is a legitimate state
    /// (it contributes zero), but it cannot tell price from pay, which is why this fixture exists.
    /// </summary>
    private static async Task SeedTwoPayableJobs(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var category = ServiceCategory.Create("cleaning-preview", "Cleaning", "Cleaning services");
        category.Id = PayableCategoryId;
        context.Add(category);

        var service = Service.Create(PayableCategoryId, "Deep clean", "A payable service", 900m, 0m, 120);
        service.Id = PayableServiceId;
        context.Add(service);

        // Platform-wide config (no employee id) — the fallback arm of the estimator, which is the arm a
        // cleaner with no per-person override actually hits.
        context.Add(EmployeePayConfig.CreateForService(PayableServiceId, PayPerJob, CurrencyId));

        context.Add(NewCleaner());

        for (var index = 0; index < 2; index++)
        {
            var order = NewOfferableOrder(index);
            order.AddSelectedServices([OrderService.Create(order, service)]);
            context.Add(order);
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task SeedTheAvailableBoard(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        context.Add(NewCleaner());

        for (var index = 0; index < SeededOrders; index++)
        {
            context.Add(NewOfferableOrder(index));
        }

        await context.CommitAsync(CancellationToken.None);
    }

    private static Order NewOfferableOrder(int index)
    {
        var order = Order.Create(
            customerName: "Preview Customer",
            customerEmail: "preview-customer@cleansia.test",
            customerPhone: "+420777111666",
            customerAddress: Address.Create(Street, City, ZipCode, CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(2).AddMinutes(index * 30),
            paymentType: PaymentType.Card,
            totalPrice: 1000m + index,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = $"order-preview-{index:D3}";
        order.UpdateEstimatedTime(120);
        order.SetMaxEmployees(1);
        order.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Confirmed, order));
        return order;
    }

    private static Employee NewCleaner()
    {
        var user = User.CreateWithPassword(
            CallerEmail,
            TestUtilities.Constants.TestUserSession.TestUserPassword,
            "Petra",
            "Preview",
            UserProfile.Employee);
        user.Id = CallerUserId;
        user.ConfirmEmail();
        user.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);

        var employee = Employee.CreateWithUser(user);
        employee.Id = CallerEmployeeId;
        employee.Approve(approvedByUserId: "admin-preview");
        employee.AssignWorkCountry(CountryId);
        employee.UpdateAddress(Address.Create("Cleaner St 9", City, ZipCode, CountryId));
        employee.Created(TestUtilities.Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        return employee;
    }
}
