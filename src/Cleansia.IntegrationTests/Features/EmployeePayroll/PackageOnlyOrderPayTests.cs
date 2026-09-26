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
/// A completed job whose only line is a PACKAGE pays the cleaner the package's configured rate.
///
/// <para>Against real Postgres, because the defect was invisible anywhere else: the handler and the
/// validator both loaded the order with <c>.Include(o =&gt; o.SelectedServices)</c> and then read
/// <c>order.SelectedPackages</c>, which EF leaves empty with no lazy loading in the solution. In a unit
/// test the collection is populated in memory by the arrangement, so the calculator sees the package
/// and the bug cannot appear. Only a real load reproduces it — the package pay config was never read,
/// the pay row was written with a zero total, and the cleaner was paid nothing.</para>
///
/// <para>The order carries no service line at all, which is the case the shipped path got wrong;
/// a mixed order was always paid for its services.</para>
/// </summary>
[Collection("PostgresCollection")]
public class PackageOnlyOrderPayTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-package-pay";
    private const string CountryId = "country-cz-package-pay";

    private const decimal PackageBasePay = 420m;
    private const decimal ExtraPerRoom = 30m;
    private const decimal ExtraPerBathroom = 20m;

    private static string _orderId = default!;
    private static string _employeeId = default!;

    [Fact]
    public async Task A_Package_Only_Job_Pays_The_Package_Rate()
    {
        await TestMethod(
            arrange: SeedCompletedPackageOnlyOrder,
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
                    .FirstAsync(p => p.OrderId == _orderId && p.EmployeeId == _employeeId);

                // Rooms 3 → two EXTRA rooms (the first is in the base), bathrooms 2, no travel distance:
                // 420 + 2×30 + 2×20 = 520.
                Assert.Equal(PackageBasePay, pay.BasePay);
                Assert.Equal((2 * ExtraPerRoom) + (2 * ExtraPerBathroom), pay.ExtrasPay);
                Assert.Equal(520m, pay.TotalPay);
                Assert.Equal(CurrencyId, pay.CurrencyId);
            });
    }

    /// <summary>
    /// The validator's own read has the same gap, and it fails in the opposite direction: with the
    /// package line invisible it finds no config for the order and rejects the command as
    /// <c>NoPayConfiguration</c> — so a package-only job could also never even reach the handler when
    /// the platform holds a package rate and nothing else.
    /// </summary>
    [Fact]
    public async Task The_Validator_Accepts_A_Package_Only_Job_Whose_Only_Config_Is_The_Packages()
    {
        await TestMethod(
            arrange: SeedCompletedPackageOnlyOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new CalculateOrderPay.Command(_orderId, _employeeId));
            },
            assert: async (CleansiaDbContext context, BusinessResult<CalculateOrderPay.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.Equal(
                    1,
                    await context.Set<OrderEmployeePay>()
                        .IgnoreQueryFilters()
                        .CountAsync(p => p.OrderId == _orderId));
            });
    }

    private static async Task SeedCompletedPackageOnlyOrder(CleansiaDbContext context)
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
            "package-pay@cleansia.test", "12345678Test!", "Pack", "Cleaner", UserProfile.Employee);
        cleaner.ConfirmEmail();
        cleaner.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(cleaner);

        var employee = Employee.CreateWithUser(cleaner);
        context.Set<Employee>().Add(employee);

        var package = Package.Create("Deep clean package", "Everything, once");
        context.Set<Package>().Add(package);

        context.Set<EmployeePayConfig>().Add(EmployeePayConfig.CreateForPackage(
            packageId: package.Id,
            basePay: PackageBasePay,
            currencyId: CurrencyId,
            extraPerRoom: ExtraPerRoom,
            extraPerBathroom: ExtraPerBathroom));

        context.Set<PayPeriod>().Add(PayPeriod.CreateBiWeekly(
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));

        var order = Order.Create(
            customerName: "Package Only",
            customerEmail: "package-customer@cleansia.test",
            customerPhone: "+420777333444",
            customerAddress: Address.Create("Package St 1", "Brno", "60200", CountryId),
            rooms: 3,
            bathrooms: 2,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 2400m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        order.AddSelectedPackages([OrderPackage.Create(order, package, 2400m)]);
        order.AddAssignedEmployee(OrderEmployee.Create(order, employee));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.MarkCompletedAt(DateTime.UtcNow);
        context.Add(order);

        await context.CommitAsync(CancellationToken.None);

        _orderId = order.Id;
        _employeeId = employee.Id;
    }
}
