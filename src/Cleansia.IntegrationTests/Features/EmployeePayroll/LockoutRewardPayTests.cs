using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// Owner decision 2026-10-04 on real Postgres: a confirmed lockout pays each seat its full contracted reward,
/// always - on a cash booking whose lockout price is still owed, and on a card order refunded in full before
/// the lockout. The row keeps the lockout line type.
///
/// <para>The seats froze 840 base and 280 extras, floored at 500 and capped at 1 000, on a job booked heavy at
/// 30 %: the capped 1 000 adds 300. A lone seat earns 1 300; two seats are each owed 500 + 150 = 650. The rate
/// in force, 600 base with no bounds, pays an uncontracted lone seat 600 + 180 = 780.</para>
/// </summary>
[Collection("PostgresCollection")]
public class LockoutRewardPayTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-lockout-pay";
    private const string CountryId = "country-cz-lockout-pay";

    private string _orderId = "";
    private string _firstSeatEmployeeId = "";
    private string _secondSeatEmployeeId = "";

    [Fact]
    public async Task A_Cash_Lockout_Whose_Price_Is_Still_Owed_Pays_Each_Seat_Its_Contracted_Reward()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var order = SeedLockedOutOrder(ctx, PaymentType.Cash, PaymentStatus.Pending, seats: 2, frozen: true);
                ctx.Receivables.Add(Receivable.ForLockout(order, order.TotalPrice));
                await ctx.CommitAsync(CancellationToken.None);
            },
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

                var first = pays[_firstSeatEmployeeId];
                Assert.Equal(PayLineType.LockoutFeeShare, first.LineType);
                Assert.Equal(
                    (420m, 140m, 150m, 250m, 500m, 650m),
                    (first.BasePay, first.ExtrasPay, first.DirtinessPay, first.MinPay, first.MaxPay, first.TotalPay));

                var second = pays[_secondSeatEmployeeId];
                Assert.Equal(PayLineType.LockoutFeeShare, second.LineType);
                Assert.Equal(650m, second.TotalPay);
            });
    }

    [Fact]
    public async Task A_Card_Order_Refunded_In_Full_Before_Its_Lockout_Still_Pays_The_Reward()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var order = SeedLockedOutOrder(ctx, PaymentType.Card, PaymentStatus.Paid, seats: 1, frozen: true);
                order.ApplyCredit(400m, order.UserId!);

                var cardLeg = Refund.Create(_orderId, "refund:admin-full", 2200m, "CZK",
                    RefundReason.AdminDiscretion, RefundSource.AppRefund);
                cardLeg.MarkSucceeded("re_admin_full", DateTimeOffset.UtcNow);
                ctx.Refunds.Add(cardLeg);
                var credit = CreditAccount.Create(order.UserId!, CurrencyId, "admin");
                credit.Issue(400m, CreditTransactionReason.OrderPaymentReturned, "credit-return:refund:admin-full",
                    "admin", orderId: _orderId);
                ctx.CreditAccounts.Add(credit);
                order.UpdatePaymentStatus(PaymentStatus.Refunded);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<IMediator>()
                .Send(new CalculateOrderPay.Command(_orderId, _firstSeatEmployeeId)),
            assert: async (CleansiaDbContext context, BusinessResult<CalculateOrderPay.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var pay = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .SingleAsync(p => p.OrderId == _orderId);
                Assert.Equal(PayLineType.LockoutFeeShare, pay.LineType);
                Assert.Equal(1300m, pay.TotalPay);
            });
    }

    [Fact]
    public async Task A_Seat_With_No_Contract_Is_Paid_At_The_Rate_In_Force()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                SeedLockedOutOrder(ctx, PaymentType.Cash, PaymentStatus.Pending, seats: 1, frozen: false);
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<IMediator>()
                .Send(new CalculateOrderPay.Command(_orderId, _firstSeatEmployeeId)),
            assert: async (CleansiaDbContext context, BusinessResult<CalculateOrderPay.Response> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var pay = await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .SingleAsync(p => p.OrderId == _orderId);
                Assert.Equal(PayLineType.LockoutFeeShare, pay.LineType);
                Assert.Equal((600m, 180m, 780m), (pay.BasePay, pay.DirtinessPay, pay.TotalPay));
            });
    }

    private Order SeedLockedOutOrder(
        CleansiaDbContext context, PaymentType paymentType, PaymentStatus paymentStatus, int seats, bool frozen)
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

        var customer = User.CreateWithPassword("lockout-reward-customer@cleansia.test", "12345678Test!", "Lockout", "Customer");
        context.Users.Add(customer);

        var package = Package.Create("Lockout reward package", "Two hours of work");
        context.Set<Package>().Add(package);
        context.Set<EmployeePayConfig>().Add(EmployeePayConfig.CreateForPackage(
            packageId: package.Id,
            basePay: 600m,
            currencyId: CurrencyId));

        context.Set<PayPeriod>().Add(PayPeriod.CreateBiWeekly(
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));

        var order = Order.Create(
            customerName: "Lockout Customer",
            customerEmail: "lockout-reward-customer@cleansia.test",
            customerPhone: "+420777333777",
            customerAddress: Address.Create("Zamcena 3", "Brno", "60200", CountryId),
            rooms: 3,
            bathrooms: 0,
            cleaningDateTime: DateTime.UtcNow.AddHours(-1),
            paymentType: paymentType,
            totalPrice: 2600m,
            currencyId: CurrencyId,
            paymentStatus: paymentStatus,
            userId: customer.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.AddSelectedPackages([OrderPackage.Create(order, package, 2000m)]);
        order.UpdateEstimatedTime(seats * 120).CalculateRequiredEmployees(spareSeats: 0);
        order.SetDirtinessSurcharge(DirtinessLevel.Heavy, 600m, 0.30m);

        var first = NewEmployee(context, "lockout-reward-first@cleansia.test");
        order.AddAssignedEmployee(OrderEmployee.Create(order, first));
        _firstSeatEmployeeId = first.Id;
        if (seats > 1)
        {
            var second = NewEmployee(context, "lockout-reward-second@cleansia.test");
            order.AddAssignedEmployee(OrderEmployee.Create(order, second));
            _secondSeatEmployeeId = second.Id;
        }

        if (frozen)
        {
            foreach (var seat in order.AssignedEmployees)
            {
                seat.FreezeJobPay((840m, 280m, 500m, 1000m));
            }
        }

        order.Cancel(DateTime.UtcNow, CancelledBy.Admin, feeRate: BookingPolicy.LockoutFeeRate, refundAmount: 0m,
            reason: OrderCancellationReasons.CustomerLockout);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        context.Add(order);
        _orderId = order.Id;
        return order;
    }

    private static Employee NewEmployee(CleansiaDbContext context, string email)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Lockout", "Cleaner", UserProfile.Employee);
        user.ConfirmEmail();
        user.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(user);

        var employee = Employee.CreateWithUser(user);
        context.Set<Employee>().Add(employee);
        return employee;
    }
}
