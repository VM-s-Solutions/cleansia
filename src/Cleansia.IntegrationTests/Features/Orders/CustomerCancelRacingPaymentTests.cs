using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// A signed-in customer cancels a card checkout they have not paid, on which 300 of credit was spent, and the card
/// payment lands while the cancel is being decided. The order's payment status is a concurrency token, so the
/// cancel's commit is refused and rolls back with its credit return: the customer gets one error, and the order
/// stays paid and booked with its credit still spent. Without the token the cancel landed on the paid order, which
/// ended cancelled with the money kept and nothing refunded, and the credit was handed back on top.
/// </summary>
[Collection("PostgresCollection")]
public class CustomerCancelRacingPaymentTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CountryId = "country-cz-paid-race";
    private const string CzkId = "cur-czk-paid-race";
    private const string CustomerEmail = "cancel-paid-race@cleansia.test";
    private const string OrderId = "order-cancel-paid-race";
    private const string ReturnKey = $"credit-return:order-ended-unpaid:{OrderId}";
    private const decimal Credit = 300m;

    private string _customerId = string.Empty;
    private bool _paid;

    [Fact]
    public async Task A_Cancel_Decided_Before_The_Payment_Landed_Is_Refused_And_The_Order_Stays_Paid()
    {
        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
                    _customerId, CustomerEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
                services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(
                    _ => new TestRequestMetadataProvider("203.0.113.10", "iPhone 15 / iOS 17.4", "device-1")));
                services.Replace(ServiceDescriptor.Scoped<ICancellationPolicyResolver>(sp => new PaidWhileCancelling(
                    ActivatorUtilities.CreateInstance<CancellationPolicyResolver>(sp), PayOnceAsync)));
                return Task.CompletedTask;
            },
            arrange: (CleansiaDbContext _) => SeedAsync(),
            act: provider => Record.ExceptionAsync(() => provider.GetRequiredService<IMediator>()
                .Send(new CancelOrder.Command(OrderId, Reason: "changed my mind"))),
            assert: async (CleansiaDbContext context, Exception? refused) =>
            {
                var order = await context.Orders.IgnoreQueryFilters().AsNoTracking()
                    .Include(o => o.OrderStatusHistory)
                    .SingleAsync(o => o.Id == OrderId);
                Assert.Equal((PaymentStatus.Paid, OrderStatus.New), (order.PaymentStatus, order.CurrentStatus));
                Assert.DoesNotContain(order.OrderStatusHistory, t => t.Status == OrderStatus.Cancelled);
                var account = await context.CreditAccounts.IgnoreQueryFilters().AsNoTracking()
                    .Include(a => a.Transactions)
                    .SingleAsync(a => a.UserId == _customerId && a.CurrencyId == CzkId);
                Assert.DoesNotContain(account.Transactions, t => t.IdempotencyKey == ReturnKey);
                Assert.Equal(0m, account.Balance);
                Assert.Equal(account.Balance, account.Transactions.Sum(t => t.Amount));

                Assert.True(_paid);
                Assert.IsType<DbUpdateConcurrencyException>(refused);
            },
            transactional: false);
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));

    /// <summary>
    /// The state an unpaid checkout leaves: a 300 grant spent on the order under its payment key, so the balance is
    /// zero and still equals the ledger, and a card order nobody has paid, still open.
    /// </summary>
    private async Task SeedAsync()
    {
        await using (var ctx = NewContext())
        {
            ctx.Languages.Add(Language.Create("en", "English"));
            var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
            country.Id = CountryId;
            ctx.Countries.Add(country);
            var currency = Currency.Create("CZK", "CZK", "CZK");
            currency.Id = CzkId;
            currency.IsActive = true;
            currency.SetAsDefault(true);
            ctx.Currencies.Add(currency);
            var customer = User.CreateWithPassword(CustomerEmail, "Seed-Password-123", "Cancel", "Race");
            ctx.Users.Add(customer);
            await ctx.CommitAsync(CancellationToken.None);
            _customerId = customer.Id;
        }

        await using (var ctx = NewContext())
        {
            var account = await new CreditAccountRepository(ctx).EnsureForUserAsync(_customerId, CzkId, CancellationToken.None);
            account!.Issue(Credit, CreditTransactionReason.Goodwill, "seed-grant", "seed", note: "n");

            var order = Order.Create(
                customerName: "Cancel Race",
                customerEmail: CustomerEmail,
                customerPhone: "+420000000000",
                customerAddress: Address.Create("123 Main St", "Prague", "11000", CountryId),
                rooms: 1,
                bathrooms: 1,
                cleaningDateTime: DateTime.UtcNow.AddDays(3),
                paymentType: PaymentType.Card,
                totalPrice: 1000m,
                currencyId: CzkId,
                paymentStatus: PaymentStatus.Pending,
                userId: _customerId,
                cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
            order.Id = OrderId;
            order.SetMaxEmployees(1);
            order.ApplyCredit(Credit, _customerId);
            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
            ctx.Orders.Add(order);
            await ctx.CommitAsync(CancellationToken.None);

            Assert.True(await new CreditAccountRepository(ctx).TryDebitAsync(
                account.Id, Credit, CreditTransactionReason.OrderPayment, $"order-payment-{OrderId}", _customerId,
                CancellationToken.None, orderId: OrderId));
        }
    }

    /// <summary>The webhook's write of the card payment, committed once on a connection of its own.</summary>
    private async Task PayOnceAsync()
    {
        if (_paid)
        {
            return;
        }

        _paid = true;
        await using var paying = NewContext();
        var order = await paying.Orders.IgnoreQueryFilters().SingleAsync(o => o.Id == OrderId);
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        await paying.CommitAsync(CancellationToken.None);
    }

    /// <summary>The cancel's first step after it loaded the order: the payment commits, then the policy is read.</summary>
    private sealed class PaidWhileCancelling(ICancellationPolicyResolver inner, Func<Task> pay) : ICancellationPolicyResolver
    {
        public async Task<CancellationPolicy> ResolveForOrderAsync(Order order, CancellationToken cancellationToken)
        {
            await pay();
            return await inner.ResolveForOrderAsync(order, cancellationToken);
        }
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
