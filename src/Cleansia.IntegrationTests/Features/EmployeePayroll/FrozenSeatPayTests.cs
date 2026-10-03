using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
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
/// Owner ruling 2026-10-03 on real Postgres: the take freezes on the seat the job figures its contract
/// reward was priced from, a re-grade after the take is committed, and the pay is still the contract's.
///
/// <para>At the take the rate is 840 base and 140 per extra room, floored at 500 and capped at 1 000; the
/// 3-room job is 840 + 2 x 140 = 1 120, capped to 1 000, and booked heavy at 30 % it adds 300, so the lone
/// seat's reward is 1 300. The re-grade to 600 and 100 with no bounds would pay 800 + 240 = 1 040.</para>
/// </summary>
[Collection("PostgresCollection")]
public class FrozenSeatPayTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-frozen-pay";
    private const string CountryId = "country-cz-frozen-pay";
    private const string OrderId = "order-frozen-pay";
    private const string EmployeeId = "employee-frozen-pay";

    [Fact]
    public async Task A_Re_Grade_After_The_Take_Does_Not_Reprice_The_Seat()
    {
        await TestMethod(
            arrange: SeedCompletedOneSeatHeavyOrder,
            act: TakeRegradeAndPayAsync,
            assert: async (CleansiaDbContext context,
                (decimal ContractReward, BusinessResult<CalculateOrderPay.Response> Pay) results) =>
            {
                Assert.True(results.Pay.IsSuccess, results.Pay.Error?.Message);

                var seat = await context.Set<OrderEmployee>()
                    .IgnoreQueryFilters()
                    .SingleAsync(oe => oe.OrderId == OrderId);
                Assert.Equal(
                    ((decimal?)840m, (decimal?)280m, (decimal?)500m, (decimal?)1000m),
                    (seat.JobBasePay, seat.JobExtrasPay, seat.JobMinPay, seat.JobMaxPay));

                var pay = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .SingleAsync(p => p.OrderId == OrderId);
                Assert.Equal(840m, pay.BasePay);
                Assert.Equal(280m, pay.ExtrasPay);
                Assert.Equal(300m, pay.DirtinessPay);
                Assert.Equal(500m, pay.MinPay);
                Assert.Equal(1000m, pay.MaxPay);
                Assert.Equal(1300m, pay.TotalPay);
                Assert.Equal(results.ContractReward, pay.TotalPay);
            },
            transactional: false);
    }

    private static async Task<(decimal ContractReward, BusinessResult<CalculateOrderPay.Response> Pay)>
        TakeRegradeAndPayAsync(IServiceProvider provider)
    {
        var scopes = provider.GetRequiredService<IServiceScopeFactory>();

        decimal contractReward;
        using (var take = scopes.CreateScope())
        {
            var (facts, jobPay) = (await take.ServiceProvider.GetRequiredService<IWorkContractFactsBuilder>()
                .BuildAsync(OrderId, EmployeeId, CancellationToken.None))!.Value;
            var context = take.ServiceProvider.GetRequiredService<CleansiaDbContext>();
            var seat = await context.Set<OrderEmployee>().SingleAsync(oe => oe.OrderId == OrderId);
            seat.FreezeJobPay(jobPay!.Value);
            await context.CommitAsync(CancellationToken.None);
            contractReward = facts.TotalPrice;
        }

        using (var regrade = scopes.CreateScope())
        {
            var context = regrade.ServiceProvider.GetRequiredService<CleansiaDbContext>();
            var rate = await context.Set<EmployeePayConfig>().SingleAsync();
            rate.UpdatePayRates(600m, 100m, 0m).SetPayLimits(0m, 0m);
            await context.CommitAsync(CancellationToken.None);
        }

        var pay = await provider.GetRequiredService<IMediator>().Send(new CalculateOrderPay.Command(OrderId, EmployeeId));
        return (contractReward, pay);
    }

    private static async Task SeedCompletedOneSeatHeavyOrder(CleansiaDbContext context)
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

        var user = User.CreateWithPassword("frozen-pay@cleansia.test", "12345678Test!", "Frozen", "Cleaner", UserProfile.Employee);
        user.ConfirmEmail();
        user.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(user);
        var cleaner = Employee.CreateWithUser(user);
        cleaner.Id = EmployeeId;
        context.Set<Employee>().Add(cleaner);

        var package = Package.Create("Frozen pay package", "Two hours of work");
        context.Set<Package>().Add(package);

        var rate = EmployeePayConfig.CreateForPackage(
            packageId: package.Id,
            basePay: 840m,
            currencyId: CurrencyId,
            extraPerRoom: 140m);
        rate.SetPayLimits(500m, 1000m);
        context.Set<EmployeePayConfig>().Add(rate);

        context.Set<PayPeriod>().Add(PayPeriod.CreateBiWeekly(
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));

        var order = Order.Create(
            customerName: "Frozen Job",
            customerEmail: "frozen-customer@cleansia.test",
            customerPhone: "+420777333666",
            customerAddress: Address.Create("Frozen St 1", "Brno", "60200", CountryId),
            rooms: 3,
            bathrooms: 0,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 2600m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.AddSelectedPackages([OrderPackage.Create(order, package, 2000m)]);
        order.UpdateEstimatedTime(120).CalculateRequiredEmployees(spareSeats: 0);
        order.SetDirtinessSurcharge(DirtinessLevel.Heavy, 600m, 0.30m);
        order.AddAssignedEmployee(OrderEmployee.Create(order, cleaner));
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        order.MarkCompletedAt(DateTime.UtcNow);
        context.Add(order);

        await context.CommitAsync(CancellationToken.None);
    }
}
