using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeePayroll;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.EmployeePayroll;

/// <summary>
/// Owner ruling 2026-09-28, decision 12: on a late-cancelled job the crew is paid half of the fee the company
/// collected, split across the seats as job pay is, as a pay line of its own type; a fee still owed pays
/// nothing. The webhook finds the crew to pay on the receivable it settles. A confirmed lockout pays the
/// seat's reward instead (<see cref="LockoutRewardPayTests"/>).
/// </summary>
[Collection("PostgresCollection")]
public class CollectedFeeSharePayTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-fee-share";
    private const string CountryId = "country-cz-fee-share";

    private string _orderId = "";
    private string _receivableId = "";
    private string _firstSeatEmployeeId = "";
    private string _secondSeatEmployeeId = "";

    [Fact]
    public async Task A_Late_Cancellation_Kept_From_A_Card_Payment_Pays_Each_Seat_Half_Of_The_Fee_Split_Like_Job_Pay()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var order = SeedCancelledOrder(ctx, PaymentType.Card, PaymentStatus.Paid, totalPrice: 1333.33m, seats: 2);
                order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0.5m, refundAmount: 666.67m, reason: null);
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

                // Kept 666.66 of 1333.33; half is 333.33, which splits into 166.67 and 166.66.
                var first = pays[_firstSeatEmployeeId];
                Assert.Equal(PayLineType.CancellationFeeShare, first.LineType);
                Assert.Equal(166.67m, first.BasePay);
                Assert.Equal(166.67m, first.TotalPay);
                Assert.Equal(CurrencyId, first.CurrencyId);
                Assert.Contains("Collected fee: 666.66", first.PayBreakdown);

                var second = pays[_secondSeatEmployeeId];
                Assert.Equal(PayLineType.CancellationFeeShare, second.LineType);
                Assert.Equal(166.66m, second.TotalPay);
            });
    }

    [Fact]
    public async Task A_Fee_Still_Owed_On_An_Open_Receivable_Pays_Nothing()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var order = SeedCancelledOrder(ctx, PaymentType.Cash, PaymentStatus.Pending, totalPrice: 900m, seats: 1);
                order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0.5m, refundAmount: 450m, reason: null);
                ctx.Receivables.Add(Receivable.ForCashCancellationFee(order, 450m));
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: provider => provider.GetRequiredService<IMediator>()
                .Send(new CalculateOrderPay.Command(_orderId, _firstSeatEmployeeId)),
            assert: async (CleansiaDbContext context, BusinessResult<CalculateOrderPay.Response> result) =>
            {
                var validation = Assert.IsAssignableFrom<IValidationResult>(result);
                Assert.Contains(validation.Errors, e => e.Message == BusinessErrorMessage.NoCollectedFee);
                Assert.False(await context.Set<OrderEmployeePay>()
                    .IgnoreQueryFilters()
                    .AnyAsync(p => p.OrderId == _orderId));
            });
    }

    [Fact]
    public async Task A_Late_Cancellation_After_A_Partial_Refund_Pays_Half_Of_What_The_Company_Still_Holds()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var order = SeedCancelledOrder(ctx, PaymentType.Card, PaymentStatus.Paid, totalPrice: 1000m, seats: 1);
                var partial = Refund.Create(_orderId, "refund:partial", 700m, "CZK",
                    RefundReason.AdminDiscretion, RefundSource.AppRefund);
                partial.MarkSucceeded("re_partial", DateTimeOffset.UtcNow);
                ctx.Refunds.Add(partial);
                order.UpdatePaymentStatus(PaymentStatus.PartiallyRefunded);

                order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0.5m, refundAmount: 500m, reason: null);
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
                Assert.Equal(150m, pay.TotalPay);
                Assert.Contains("Collected fee: 300.00", pay.PayBreakdown);
            });
    }

    [Fact]
    public async Task The_Receivable_The_Webhook_Settles_Carries_The_Crew_Of_Its_Order()
    {
        await TestMethod(
            arrange: async ctx =>
            {
                var order = SeedCancelledOrder(ctx, PaymentType.Cash, PaymentStatus.Pending, totalPrice: 900m, seats: 2);
                order.Cancel(DateTime.UtcNow, CancelledBy.Customer, feeRate: 0.5m, refundAmount: 450m, reason: null);
                var receivable = Receivable.ForCashCancellationFee(order, 450m);
                ctx.Receivables.Add(receivable);
                _receivableId = receivable.Id;
                await ctx.CommitAsync(CancellationToken.None);
            },
            act: async provider =>
            {
                var receivable = await provider.GetRequiredService<IReceivableRepository>()
                    .GetByIdIgnoringTenantAsync(_receivableId, CancellationToken.None);
                return receivable!.Order!.AssignedEmployees.Select(a => a.EmployeeId).ToHashSet();
            },
            assert: (CleansiaDbContext _, HashSet<string> crew) =>
            {
                Assert.Equal(new HashSet<string> { _firstSeatEmployeeId, _secondSeatEmployeeId }, crew);
                return Task.CompletedTask;
            });
    }

    private Order SeedCancelledOrder(
        CleansiaDbContext context, PaymentType paymentType, PaymentStatus paymentStatus, decimal totalPrice, int seats)
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

        var customer = User.CreateWithPassword("fee-share-customer@cleansia.test", "12345678Test!", "Fee", "Customer");
        context.Users.Add(customer);

        context.Set<PayPeriod>().Add(PayPeriod.CreateBiWeekly(
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-3))));

        var order = Order.Create(
            customerName: "Fee Customer",
            customerEmail: "fee-share-customer@cleansia.test",
            customerPhone: "+420777333666",
            customerAddress: Address.Create("Poplatkova 2", "Brno", "60200", CountryId),
            rooms: 1,
            bathrooms: 0,
            cleaningDateTime: DateTime.UtcNow.AddHours(2),
            paymentType: paymentType,
            totalPrice: totalPrice,
            currencyId: CurrencyId,
            paymentStatus: paymentStatus,
            userId: customer.Id,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.UpdateEstimatedTime(seats * 120).CalculateRequiredEmployees(spareSeats: 0);

        var first = NewEmployee(context, "fee-share-first@cleansia.test");
        order.AddAssignedEmployee(OrderEmployee.Create(order, first));
        _firstSeatEmployeeId = first.Id;
        if (seats > 1)
        {
            var second = NewEmployee(context, "fee-share-second@cleansia.test");
            order.AddAssignedEmployee(OrderEmployee.Create(order, second));
            _secondSeatEmployeeId = second.Id;
        }

        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));
        context.Add(order);
        _orderId = order.Id;
        return order;
    }

    private static Employee NewEmployee(CleansiaDbContext context, string email)
    {
        var user = User.CreateWithPassword(email, "12345678Test!", "Fee", "Cleaner", UserProfile.Employee);
        user.ConfirmEmail();
        user.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(user);

        var employee = Employee.CreateWithUser(user);
        context.Set<Employee>().Add(employee);
        return employee;
    }
}
