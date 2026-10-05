using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
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
/// lands on the first seat whichever row is calculated first. The term is raised by the rate the order
/// was booked at, read back from the order's own column.
/// </summary>
[Collection("PostgresCollection")]
public class CrewSeatPayTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-crew-pay";
    private const string CountryId = "country-cz-crew-pay";
    private const decimal EarlierHeavyRate = 0.60m;

    private static string _orderId = default!;
    private static string _firstSeatEmployeeId = default!;
    private static string _secondSeatEmployeeId = default!;

    [Fact]
    public async Task Each_Seat_Is_Paid_Its_Share_Of_The_Job_Raised_By_The_Level()
    {
        await TestMethod(
            arrange: context => SeedCompletedTwoSeatHeavyOrder(context, bookedRate: 0.30m),
            act: PayBothSeatsSecondFirst,
            assert: async (CleansiaDbContext context,
                (BusinessResult<CalculateOrderPay.Response> First, BusinessResult<CalculateOrderPay.Response> Second) results) =>
            {
                Assert.True(results.First.IsSuccess, results.First.Error?.Message);
                Assert.True(results.Second.IsSuccess, results.Second.Error?.Message);

                var pays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.OrderId == _orderId)
                    .ToDictionaryAsync(p => p.EmployeeId);

                // Job 333.33, heavy adds 100.00 (30 %, rounded): 166.67 + 50.00 and 166.66 + 50.00.
                var first = pays[_firstSeatEmployeeId];
                Assert.Equal(166.67m, first.BasePay);
                Assert.Equal(50m, first.DirtinessPay);
                Assert.Equal(216.67m, first.TotalPay);
                Assert.Contains("Dirtiness", first.PayBreakdown);

                var second = pays[_secondSeatEmployeeId];
                Assert.Equal(166.66m, second.BasePay);
                Assert.Equal(50m, second.DirtinessPay);
                Assert.Equal(216.66m, second.TotalPay);
            });
    }

    [Fact]
    public async Task Each_Seat_Is_Raised_By_The_Rate_The_Order_Was_Booked_At_Not_Todays()
    {
        Assert.NotEqual(BookingPolicy.HeavyDirtinessSurchargeRate, EarlierHeavyRate);

        await TestMethod(
            arrange: context => SeedCompletedTwoSeatHeavyOrder(context, EarlierHeavyRate),
            act: PayBothSeatsSecondFirst,
            assert: async (CleansiaDbContext context,
                (BusinessResult<CalculateOrderPay.Response> First, BusinessResult<CalculateOrderPay.Response> Second) results) =>
            {
                Assert.True(results.First.IsSuccess, results.First.Error?.Message);
                Assert.True(results.Second.IsSuccess, results.Second.Error?.Message);

                var pays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.OrderId == _orderId)
                    .ToDictionaryAsync(p => p.EmployeeId);

                // Job 333.33, booked heavy at 60 % adds 200.00 (199.998, rounded): 166.67 + 100.00 and
                // 166.66 + 100.00. Today's 30 % would pay 50.00 a seat.
                var first = pays[_firstSeatEmployeeId];
                Assert.Equal(100m, first.DirtinessPay);
                Assert.Equal(266.67m, first.TotalPay);

                var second = pays[_secondSeatEmployeeId];
                Assert.Equal(100m, second.DirtinessPay);
                Assert.Equal(266.66m, second.TotalPay);
            });
    }

    /// <summary>
    /// Owner decision 2026-10-04: an extra booked at 250 pays the company's default half of its price, 125,
    /// inside the job's extras. The job is 333.33 + 125 = 458.33 and heavy adds 137.50 (30 %, rounded): the
    /// first seat is paid 166.67 + 62.50 + 68.75 and the second 166.66 + 62.50 + 68.75.
    /// </summary>
    [Fact]
    public async Task Each_Seat_Is_Paid_Its_Share_Of_The_Extras_Booked()
    {
        await TestMethod(
            arrange: context => SeedCompletedTwoSeatHeavyOrder(context, bookedRate: 0.30m, extraPrice: 250m),
            act: PayBothSeatsSecondFirst,
            assert: async (CleansiaDbContext context,
                (BusinessResult<CalculateOrderPay.Response> First, BusinessResult<CalculateOrderPay.Response> Second) results) =>
            {
                Assert.True(results.First.IsSuccess, results.First.Error?.Message);
                Assert.True(results.Second.IsSuccess, results.Second.Error?.Message);

                var pays = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .Where(p => p.OrderId == _orderId)
                    .ToDictionaryAsync(p => p.EmployeeId);

                var first = pays[_firstSeatEmployeeId];
                Assert.Equal(
                    (166.67m, 62.50m, 68.75m, 297.92m),
                    (first.BasePay, first.ExtrasPay, first.DirtinessPay, first.TotalPay));

                var second = pays[_secondSeatEmployeeId];
                Assert.Equal(
                    (166.66m, 62.50m, 68.75m, 297.91m),
                    (second.BasePay, second.ExtrasPay, second.DirtinessPay, second.TotalPay));
            });
    }

    private static async Task<(BusinessResult<CalculateOrderPay.Response> First, BusinessResult<CalculateOrderPay.Response> Second)>
        PayBothSeatsSecondFirst(IServiceProvider provider)
    {
        var mediator = provider.GetRequiredService<IMediator>();
        var second = await mediator.Send(new CalculateOrderPay.Command(_orderId, _secondSeatEmployeeId));
        var first = await mediator.Send(new CalculateOrderPay.Command(_orderId, _firstSeatEmployeeId));
        return (first, second);
    }

    private static async Task SeedCompletedTwoSeatHeavyOrder(
        CleansiaDbContext context, decimal bookedRate, decimal? extraPrice = null)
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
            totalPrice: 3120m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddSelectedPackages([OrderPackage.Create(order, package, 2400m)]);
        if (extraPrice is { } price)
        {
            var extra = Extra.Create("crew-pay-oven", "Inside oven", null);
            context.Set<Extra>().Add(extra);
            order.AddSelectedExtras([OrderExtra.Create(order, extra, price)]);
        }
        order.UpdateEstimatedTime(240).CalculateRequiredEmployees(spareSeats: 0);
        order.SetDirtinessSurcharge(DirtinessLevel.Heavy, 2400m * bookedRate, bookedRate);
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
