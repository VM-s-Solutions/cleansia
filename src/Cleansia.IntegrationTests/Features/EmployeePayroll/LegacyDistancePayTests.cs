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
/// The pay row written for a completed job carries no distance component, even when the order holds a
/// stored travel distance and both the shared and the cleaner's own rate still hold a legacy kilometre
/// rate. Against real Postgres so the stored row, not the calculator's return value, is what is read.
/// </summary>
[Collection("PostgresCollection")]
public class LegacyDistancePayTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-legacy-km";
    private const string CountryId = "country-cz-legacy-km";

    private static string _orderId = default!;
    private static string _employeeId = default!;

    [Fact]
    public async Task The_Stored_Pay_Has_No_Distance_Component()
    {
        await TestMethod(
            arrange: SeedCompletedOrderWithLegacyDistance,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new CalculateOrderPay.Command(_orderId, _employeeId));
            },
            assert: async (CleansiaDbContext context, BusinessResult<CalculateOrderPay.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var pay = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .SingleAsync(p => p.OrderId == _orderId && p.EmployeeId == _employeeId);

                // The cleaner's own rate: 420 + 2 extra rooms × 30 + 2 bathrooms × 20. A distance term
                // would add 12.5 km × 5 on top.
                Assert.Equal(420m, pay.BasePay);
                Assert.Equal(100m, pay.ExtrasPay);
                Assert.Equal(0m, pay.ExpensesPay);
                Assert.Equal(520m, pay.TotalPay);
                Assert.DoesNotContain("Expenses", pay.PayBreakdown);
                Assert.DoesNotContain("Distance", pay.PayBreakdown);
            });
    }

    private static async Task SeedCompletedOrderWithLegacyDistance(CleansiaDbContext context)
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

        var cleaner = User.CreateWithPassword(
            "legacy-distance@cleansia.test", "12345678Test!", "Legacy", "Cleaner", UserProfile.Employee);
        cleaner.ConfirmEmail();
        cleaner.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(cleaner);

        var employee = Employee.CreateWithUser(cleaner);
        context.Set<Employee>().Add(employee);

        var package = Package.Create("Deep clean package", "Everything, once");
        context.Set<Package>().Add(package);

        var shared = EmployeePayConfig.CreateForPackage(
            packageId: package.Id,
            basePay: 300m,
            currencyId: CurrencyId,
            extraPerRoom: 10m,
            extraPerBathroom: 10m);
        SetLegacyDistanceRate(shared, 7m);
        context.Set<EmployeePayConfig>().Add(shared);

        var own = EmployeePayConfig.CreateForPackage(
            packageId: package.Id,
            basePay: 420m,
            currencyId: CurrencyId,
            extraPerRoom: 30m,
            extraPerBathroom: 20m,
            employeeId: employee.Id);
        SetLegacyDistanceRate(own, 5m);
        context.Set<EmployeePayConfig>().Add(own);

        context.Set<PayPeriod>().Add(PayPeriod.CreateBiWeekly(
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));

        var order = Order.Create(
            customerName: "Legacy Distance",
            customerEmail: "legacy-distance-customer@cleansia.test",
            customerPhone: "+420777333555",
            customerAddress: Address.Create("Distance St 1", "Brno", "60200", CountryId),
            rooms: 3,
            bathrooms: 2,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 2400m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        typeof(Order).GetProperty(nameof(Order.TravelDistance))!.SetValue(order, 12.5m);
        order.AddSelectedPackages([OrderPackage.Create(order, package, 2400m)]);
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.MarkCompletedAt(DateTime.UtcNow);
        context.Add(order);

        await context.CommitAsync(CancellationToken.None);

        _orderId = order.Id;
        _employeeId = employee.Id;
    }

    private static void SetLegacyDistanceRate(EmployeePayConfig config, decimal rate) =>
        typeof(EmployeePayConfig).GetProperty(nameof(EmployeePayConfig.DistanceRatePerKm))!
            .SetValue(config, rate);
}
