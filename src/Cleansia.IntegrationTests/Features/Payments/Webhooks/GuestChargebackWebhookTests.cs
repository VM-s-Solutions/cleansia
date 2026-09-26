using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// A bank chargeback raised against a GUEST booking, end to end through the real pipeline over real
/// Postgres.
///
/// <para>It used to be the one Stripe event the platform could not survive. A guest order has no
/// <c>UserId</c>, the webhook substituted the empty string, and <c>Disputes.UserId</c> was a required
/// foreign key into <c>Users</c> — so Postgres raised 23503 at the pipeline's commit, the webhook
/// answered 500, Stripe retried it on its own schedule forever, and the chargeback was never recorded
/// anywhere an administrator could act on it. Nothing caught it because every other dispute fixture in
/// the suite arranges a signed-in customer.</para>
///
/// <para>Postgres is load-bearing here: the defect WAS the foreign key. An in-memory double would
/// accept the empty string and the test would pass against the bug.</para>
/// </summary>
[Collection("PostgresCollection")]
public class GuestChargebackWebhookTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    // Both stand in for a ULID, so they have to fit varchar(26).
    private const string CurrencyId = "currency-czk-guest-cb";
    private const string CountryId = "country-cz-guest-cb";
    private const string PaymentIntentId = "pi_guest_chargeback";
    private const string StripeDisputeId = "dp_guest_chargeback";

    private static string _orderId = default!;

    [Fact]
    public async Task A_Chargeback_On_A_Guest_Order_Is_Recorded_And_Acknowledged()
    {
        await TestMethod(
            arrange: SeedPaidGuestCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(SignedChargebackCommand("evt_guest_chargeback"));
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var dispute = await context.Set<Dispute>()
                    .IgnoreQueryFilters()
                    .SingleAsync(d => d.OrderId == _orderId);

                Assert.Null(dispute.UserId);
                Assert.Equal(StripeDisputeId, dispute.StripeDisputeId);
                Assert.Equal(DisputeReason.Chargeback, dispute.Reason);
                Assert.Equal(DisputeStatus.Escalated, dispute.Status);
                Assert.Equal(TestTenants.Default, dispute.TenantId);

                Assert.Equal(
                    1,
                    await context.Set<ProcessedStripeEvent>()
                        .IgnoreQueryFilters()
                        .CountAsync(e => e.StripeEventId == "evt_guest_chargeback"));
            });
    }

    /// <summary>
    /// The redelivery Stripe sends when the first one 500s. It must land on one dispute, not two —
    /// the claim is what makes the acknowledgement above safe to give.
    /// </summary>
    [Fact]
    public async Task A_Redelivered_Guest_Chargeback_Records_Exactly_One_Dispute()
    {
        await TestMethod(
            arrange: SeedPaidGuestCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var first = await mediator.Send(SignedChargebackCommand("evt_guest_chargeback_replay"));
                var second = await mediator.Send(SignedChargebackCommand("evt_guest_chargeback_replay"));
                return (first, second);
            },
            assert: async (CleansiaDbContext context, (BusinessResult First, BusinessResult Second) r) =>
            {
                Assert.True(r.First.IsSuccess, r.First.Error?.Message);
                Assert.True(r.Second.IsSuccess, r.Second.Error?.Message);

                Assert.Equal(
                    1,
                    await context.Set<Dispute>().IgnoreQueryFilters().CountAsync(d => d.OrderId == _orderId));
                Assert.Equal(
                    1,
                    await context.Set<ProcessedStripeEvent>()
                        .IgnoreQueryFilters()
                        .CountAsync(e => e.StripeEventId == "evt_guest_chargeback_replay"));
            });
    }

    private static HandlePaymentNotification.Command SignedChargebackCommand(string eventId)
    {
        var body = StripeWebhookTestPayloads.ChargeDisputeCreatedBody(eventId, StripeDisputeId, PaymentIntentId);
        var signature = StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.ConfiguredWebhookSecret);
        return new HandlePaymentNotification.Command(body, signature);
    }

    /// <summary>A card order booked WITHOUT an account — the shape that has no user to name.</summary>
    private static async Task SeedPaidGuestCardOrder(CleansiaDbContext context)
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

        var order = Order.Create(
            customerName: "Guest Booker",
            customerEmail: "guest-chargeback@cleansia.test",
            customerPhone: "+420777555666",
            customerAddress: Address.Create("Guest St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-2),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Paid);
        order.AssignStripePaymentIntentId(PaymentIntentId);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Completed, order));
        context.Add(order);

        await context.CommitAsync(CancellationToken.None);

        _orderId = order.Id;
    }
}
