using System.Security.Claims;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Orders;

/// <summary>
/// A signed-in customer cancels a paid booking nobody took, ten minutes after its start and before the unfilled
/// sweep reaches it. Through the real pipeline on Postgres with the real refund seam, the cancel is paid as the
/// sweep pays — the whole refund on the sweep's key and one apology credit — keeps the no-cleaner reason key with
/// the reason the customer typed beside it, and leaves the sweep nothing to cancel or credit.
/// </summary>
[Collection("PostgresCollection")]
public class CustomerCancelOfAnUnfilledBookingTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CustomerId = "user-unfilled-cancel";
    private const string Email = "unfilled-self-cancel@cleansia.test";
    private const string CountryId = "country-cz-unfilled";
    private const string CzkId = "cur-czk-unfilled";
    private const string OrderId = "order-unfilled-cancel";
    private const string PaymentIntentId = "pi_unfilled_self_cancel";
    private const string TypedReason = "Nobody took it and I have to leave.";

    private readonly Mock<IStripeClient> _stripe = new();

    [Fact]
    public async Task A_Paid_Booking_Nobody_Took_Cancelled_By_Its_Customer_After_Its_Start_Pays_Once_And_Leaves_The_Sweep_Nothing()
    {
        await TestMethod(
            setup: services =>
            {
                services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
                    CustomerId, Email, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
                var factory = new Mock<IStripeClientFactory>();
                factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
                services.Replace(ServiceDescriptor.Singleton(factory.Object));
                _stripe.Setup(s => s.RefundPaymentIntentAsync(
                        PaymentIntentId, It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync("re_test");
                return Task.CompletedTask;
            },
            arrange: SeedAsync,
            act: async provider =>
            {
                var cancel = await SendAsync(provider, new CancelOrder.Command(OrderId, TypedReason));
                var sweep = await SendAsync(provider, new CancelUnfilledOrders.Command(GraceMinutes: 0));
                return (cancel, sweep);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<CancelOrder.Response> Cancel, BusinessResult<CancelUnfilledOrders.Response> Sweep) walk) =>
            {
                Assert.True(walk.Cancel.IsSuccess, walk.Cancel.Error?.Message);
                Assert.Equal(0m, walk.Cancel.Value!.FeeRate);
                Assert.Equal(1000m, walk.Cancel.Value.ActualRefundAmount);
                Assert.True(walk.Sweep.IsSuccess, walk.Sweep.Error?.Message);
                Assert.Equal(0, walk.Sweep.Value!.CancelledCount);
                Assert.Equal(0, walk.Sweep.Value.CreditedCount);

                var order = await context.Orders.IgnoreQueryFilters().AsNoTracking()
                    .Include(o => o.OrderStatusHistory)
                    .SingleAsync(o => o.Id == OrderId);
                Assert.Equal(OrderStatus.Cancelled, order.CurrentStatus);
                Assert.Equal(CancelledBy.Customer, order.CancelledBy);
                Assert.Equal(OrderCancellationReasons.NoCleanerAvailable, order.CancellationReason);
                Assert.Equal(TypedReason, order.CustomerCancellationReason);
                Assert.Single(order.OrderStatusHistory, s => s.Status == OrderStatus.Cancelled);

                var refund = Assert.Single(await context.Refunds.IgnoreQueryFilters().AsNoTracking()
                    .Where(r => r.OrderId == OrderId).ToListAsync());
                Assert.Equal($"refund:{OrderId}:admin", refund.RefundKey);
                Assert.Equal(RefundStatus.Succeeded, refund.Status);
                Assert.Equal(1000m, refund.Amount);
                _stripe.Verify(s => s.RefundPaymentIntentAsync(
                    PaymentIntentId, 1000m, $"refund:{OrderId}:admin", It.IsAny<CancellationToken>()), Times.Once);

                var apology = Assert.Single(await context.CreditTransactions.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.OrderId == OrderId && t.Reason == CreditTransactionReason.CleanerNoShow).ToListAsync());
                Assert.Equal($"cleaner-noshow:{OrderId}", apology.IdempotencyKey);
                Assert.Equal(250m, apology.Amount);

                Assert.Single(await context.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
                    .Where(m => m.QueueName == QueueNames.SendEmail
                        && m.MessageKey == MessageKeys.GuestOrderCancelledEmail(OrderId))
                    .ToListAsync());
            },
            transactional: false);
    }

    private static async Task<TResponse> SendAsync<TResponse>(IServiceProvider provider, IRequest<TResponse> request)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    /// <summary>A 1000 card booking, paid two days ago, nobody on it, whose start was ten minutes ago.</summary>
    private static async Task SeedAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var country = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CzkId;
        currency.IsActive = true;
        currency.SetAsDefault(true);
        currency.SetNoShowCredit(250m);
        context.Currencies.Add(currency);
        var customer = User.CreateWithPassword(Email, "Seed-Password-123", "Unfilled", "Customer");
        customer.Id = CustomerId;
        customer.ConfirmEmail();
        context.Users.Add(customer);

        var order = Order.Create(
            customerName: "Unfilled Customer",
            customerEmail: Email,
            customerPhone: "+420000000011",
            customerAddress: Address.Create("Dlouha 1", "Praha", "11000", CountryId),
            rooms: 1,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddMinutes(-10),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: CzkId,
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = OrderId;
        order.Created("seed", DateTimeOffset.UtcNow.AddDays(-2));
        order.SetMaxEmployees(1);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        context.Orders.Add(order);
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }
}
