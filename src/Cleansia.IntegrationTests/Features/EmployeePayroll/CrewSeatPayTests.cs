using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// A two-seat heavy job pays each seat half the job and half the dirtiness term, and the cent residue
/// lands on the first seat whichever row is calculated first.
/// </summary>
[Collection("PostgresCollection")]
public class CrewSeatPayTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-crew-pay";
    private const string CountryId = "country-cz-crew-pay";

    private static string _orderId = default!;
    private static string _firstSeatEmployeeId = default!;
    private static string _secondSeatEmployeeId = default!;

    [Fact]
    public async Task Each_Seat_Is_Paid_Its_Share_Of_The_Job_Raised_By_The_Level()
    {
        await TestMethod(
            arrange: SeedCompletedTwoSeatHeavyOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var second = await mediator.Send(new CalculateOrderPay.Command(_orderId, _secondSeatEmployeeId));
                var first = await mediator.Send(new CalculateOrderPay.Command(_orderId, _firstSeatEmployeeId));
                return (first, second);
            },
            assert: async (CleansiaDbContext context,
                (BusinessResult<CalculateOrderPay.Response> First, BusinessResult<CalculateOrderPay.Response> Second) results) =>
            {
                Assert.True(results.First.IsSuccess, results.First.Error?.Message);
                Assert.True(results.Second.IsSuccess, results.Second.Error?.Message);

                var pays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.OrderId == _orderId)
                    .ToDictionaryAsync(p => p.EmployeeId);

                // Job 333.33, heavy adds 200.00 (60 %, rounded): 166.67 + 100.00 and 166.66 + 100.00.
                var first = pays[_firstSeatEmployeeId];
                Assert.Equal(166.67m, first.BasePay);
                Assert.Equal(100m, first.DirtinessPay);
                Assert.Equal(266.67m, first.TotalPay);
                Assert.Contains("Dirtiness", first.PayBreakdown);

                var second = pays[_secondSeatEmployeeId];
                Assert.Equal(166.66m, second.BasePay);
                Assert.Equal(100m, second.DirtinessPay);
                Assert.Equal(266.66m, second.TotalPay);
            });
    }

    private static async Task SeedCompletedTwoSeatHeavyOrder(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.IsActive = true;
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var firstCleaner = NewEmployee(context, "crew-pay-first@cleansia.test");
        var secondCleaner = NewEmployee(context, "crew-pay-second@cleansia.test");

        var package = Package.Create("Crew clean package", "Four hours of work");
        context.Set<Package>().Add(package);

        context.Set<EmployeePayConfig>().Add(EmployeePayConfig.CreateForPackage(
            packageId: package.Id,
            basePay: 333.33m,
            currencyId: CurrencyId));

        context.Set<PayPeriod>().Add(PayPeriod.CreateBiWeekly(
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));

        var order = Order.Create(
            customerName: "Crew Job",
            customerEmail: "crew-customer@cleansia.test",
            customerPhone: "+420777333555",
            customerAddress: Address.Create("Crew St 1", "Brno", "60200", CountryId),
            rooms: 1,
            bathrooms: 0,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 3840m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        order.AddSelectedPackages([OrderPackage.Create(order, package, 2400m)]);
        order.UpdateEstimatedTime(240).CalculateRequiredEmployees(spareSeats: 0);
        order.SetDirtinessSurcharge(DirtinessLevel.Heavy, 1440m);
        order.AddAssignedEmployee(OrderEmployee.Create(order, firstCleaner));
        order.AddAssignedEmployee(OrderEmployee.Create(order, secondCleaner));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.MarkCompletedAt(DateTime.UtcNow);
        context.Add(order);

        await context.CommitAsync(CancellationToken.None);

        _orderId = order.Id;
        _firstSeatEmployeeId = firstCleaner.Id;
        _secondSeatEmployeeId = secondCleaner.Id;
    }

    private static Employee NewEmployee(CleansiaDbContext context, string email)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Crew", "Cleaner", UserProfile.Employee);
        user.ConfirmEmail();
        user.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(user);

        var employee = Employee.CreateWithUser(user);
        context.Set<Employee>().Add(employee);
        return employee;
    }
}
