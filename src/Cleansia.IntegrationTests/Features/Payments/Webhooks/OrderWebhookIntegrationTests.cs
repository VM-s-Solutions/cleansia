using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Payments;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Payments.Webhooks;

/// <summary>
/// Re-delivery idempotency and signature-rejection for the <b>order</b> webhook, driven END-TO-END
/// through the real host (<c>IMediator.Send(HandlePaymentNotification.Command)</c> → Validation →
/// UnitOfWork commit → Handler → post-commit dispatch) over a REAL Postgres (Testcontainers). This is
/// the integration depth above the mocked-handler/idempotency unit suites
/// (<c>HandleChargebackNotificationTests</c>, <c>PostCommitDispatchBehaviorTests</c>): it asserts the
/// persisted money/membership effect through the DB + the DURABLE OUTBOX seam (ADR-0010), not method
/// presence on a mock.
///
/// The dispatch seam at the current durable-outbox substrate (ADR-0010) is the <see cref="OutboxMessage"/>
/// row: <c>OutboxPendingDispatch.Enqueue</c> writes one row per (QueueName, MessageKey) into the same
/// scoped DbContext the pipeline commits, so a committed success persists exactly one outbox row per
/// effect and <c>PostCommitDispatchBehavior</c> never touches <c>IQueueClient.SendAsync</c> here (Drain
/// is empty for the durable backing). So AC1's "fires exactly once" is asserted as an outbox row count,
/// which is the current seam, rather than a raw queue-client call count (ticket staleness note §3).
///
/// The webhook is anonymous (no tenant claim): the handler reads the order tenant-ignoring
/// (<c>GetByIdIgnoringTenantAsync</c>) and the persisted effect rows carry the order's own tenant, read
/// back from the enqueued envelope body by <c>OutboxPendingDispatch</c> — asserted explicitly so the
/// row's tenant and its body stay in agreement. These run in single-tenant mode (the production web
/// Checkout path): the order-exists VALIDATOR rule is tenant-scoped, so it resolves only single-tenant
/// orders today (see productionBugsFound — the handler read is tenant-ignoring, the validator is not).
/// </summary>
[Collection("PostgresCollection")]
public class OrderWebhookIntegrationTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CurrencyId = "currency-czk-order-webhook";
    private const string CountryId = "country-cz-order-webhook";

    private static string _orderId = default!;
    private static string _userId = default!;

    // ── AC1 — a valid first delivery confirms+pays the order and persists each effect EXACTLY ONCE ──

    [Fact]
    public async Task ValidCheckoutCompleted_FirstDelivery_ConfirmsPaysOrder_AndPersistsEachEffectOnce()
    {
        await TestMethod(
            arrange: SeedPendingCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(SignedCompletedCommand("evt_order_first"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsSuccess);

                var order = await LoadOrderAsync(context);
                Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
                Assert.Equal(OrderStatus.Confirmed, LatestStatus(order));

                Assert.Equal(1, await ProcessedEventCountAsync(context, "evt_order_first"));
                Assert.Equal(1, await ReceiptOutboxCountAsync(context));
                Assert.Equal(1, await OrderConfirmedPushOutboxCountAsync(context));
            });
    }

    // ── AC1 — RE-DELIVERY: the SAME event id twice produces exactly ONE effect (claim-before-act) ──

    [Fact]
    public async Task ValidCheckoutCompleted_DeliveredTwice_ProducesExactlyOneEffect()
    {
        await TestMethod(
            arrange: SeedPendingCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                // At-least-once redelivery: identical event id, valid signature, posted twice.
                var first = await mediator.Send(SignedCompletedCommand("evt_order_redeliver"));
                var second = await mediator.Send(SignedCompletedCommand("evt_order_redeliver"));
                return (first, second);
            },
            assert: async (CleansiaDbContext context, (BusinessResult<string> First, BusinessResult<string> Second) r) =>
            {
                // The second POST returns success via the already-processed short-circuit (ADR-0002 D1.2).
                Assert.True(r.First.IsSuccess);
                Assert.True(r.Second.IsSuccess);

                var order = await LoadOrderAsync(context);
                Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
                // Exactly one Confirmed transition — the redelivery did not stack a second.
                Assert.Equal(1, order.OrderStatusHistory.Count(s => s.Status == OrderStatus.Confirmed));

                // The ProcessedStripeEvent stamp + each outbox effect survive both deliveries exactly once.
                Assert.Equal(1, await ProcessedEventCountAsync(context, "evt_order_redeliver"));
                Assert.Equal(1, await ReceiptOutboxCountAsync(context));
                Assert.Equal(1, await OrderConfirmedPushOutboxCountAsync(context));
            });
    }

    // ── AC1 — the persisted effect row carries the order's tenant, read back from the envelope body ──

    [Fact]
    public async Task ValidCheckoutCompleted_EffectRow_CarriesTheOrdersTenant()
    {
        await TestMethod(
            arrange: SeedPendingCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(SignedCompletedCommand("evt_order_tenant"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsSuccess);
                var order = await LoadOrderAsync(context);
                var receipt = await ReceiptOutboxRowAsync(context);
                // OutboxPendingDispatch derives the row's tenant from the envelope body's tenantId, so the
                // row tenant equals the order's tenant (null in single-tenant) — a regression that wrote a
                // different tenant onto the row, or lost the body's tenant, fails here.
                Assert.Equal(order.TenantId, receipt.TenantId);
            });
    }

    // ── AC4 — missing Stripe-Signature header is rejected: no state change, no stamp, no dispatch ──

    [Fact]
    public async Task MissingSignature_IsRejected_NoStateChange_NoStamp_NoDispatch()
    {
        await TestMethod(
            arrange: SeedPendingCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var body = StripeWebhookTestPayloads.CheckoutSessionCompletedBody("evt_order_nosig", _orderId);
                // No Stripe-Signature header (empty) — mirrors a request that skipped signing entirely.
                return await mediator.Send(new HandlePaymentNotification.Command(body, string.Empty));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsFailure);
                await AssertNoEffectAsync(context, "evt_order_nosig");
            });
    }

    // ── AC5 — a forged body signed with the WRONG secret is rejected: no side effect fires ──

    [Fact]
    public async Task ForgedSignature_WrongSecret_IsRejected_NoSideEffect()
    {
        await TestMethod(
            arrange: SeedPendingCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var body = StripeWebhookTestPayloads.CheckoutSessionCompletedBody("evt_order_forged", _orderId);
                // Attacker-minted event: a real HMAC, but over a secret the host is NOT configured with.
                var forged = StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.WrongWebhookSecret);
                return await mediator.Send(new HandlePaymentNotification.Command(body, forged));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsFailure);
                await AssertNoEffectAsync(context, "evt_order_forged");
            });
    }

    // ── AC7 — the valid happy path still passes, proving AC4/AC5 reject only BAD signatures ──

    [Fact]
    public async Task ValidSignature_FirstDelivery_IsProcessed_ProvingTheLockRejectsOnlyBadSignatures()
    {
        await TestMethod(
            arrange: SeedPendingCardOrder,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(SignedCompletedCommand("evt_order_happy"));
            },
            assert: async (CleansiaDbContext context, BusinessResult<string> result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.Equal(PaymentStatus.Paid, (await LoadOrderAsync(context)).PaymentStatus);
                Assert.Equal(1, await ProcessedEventCountAsync(context, "evt_order_happy"));
            });
    }

    private static HandlePaymentNotification.Command SignedCompletedCommand(string eventId)
    {
        var body = StripeWebhookTestPayloads.CheckoutSessionCompletedBody(eventId, _orderId);
        var signature = StripeWebhookTestPayloads.Sign(body, StripeWebhookTestPayloads.ConfiguredWebhookSecret);
        return new HandlePaymentNotification.Command(body, signature);
    }

    private static async Task AssertNoEffectAsync(CleansiaDbContext context, string eventId)
    {
        var order = await LoadOrderAsync(context);
        // Order stays exactly as seeded — still Pending, no Confirmed transition.
        Assert.Equal(PaymentStatus.Pending, order.PaymentStatus);
        Assert.DoesNotContain(order.OrderStatusHistory, s => s.Status == OrderStatus.Confirmed);

        Assert.Equal(0, await ProcessedEventCountAsync(context, eventId));
        Assert.Equal(0, await ReceiptOutboxCountAsync(context));
        Assert.Equal(0, await OrderConfirmedPushOutboxCountAsync(context));
    }

    private static Task<Order> LoadOrderAsync(CleansiaDbContext context) =>
        context.Set<Order>()
            .IgnoreQueryFilters()
            .Include(o => o.OrderStatusHistory)
            .FirstAsync(o => o.Id == _orderId);

    private static OrderStatus LatestStatus(Order order) =>
        order.OrderStatusHistory.OrderByDescending(s => s.CreatedOn).First().Status;

    private static async Task<int> ProcessedEventCountAsync(CleansiaDbContext context, string eventId) =>
        await context.Set<ProcessedStripeEvent>()
            .IgnoreQueryFilters()
            .CountAsync(e => e.StripeEventId == eventId);

    private static async Task<int> ReceiptOutboxCountAsync(CleansiaDbContext context) =>
        await context.OutboxMessages
            .IgnoreQueryFilters()
            .CountAsync(m => m.QueueName == QueueNames.GenerateReceipt
                          && m.MessageKey == MessageKeys.Receipt(_orderId));

    private static async Task<OutboxMessage> ReceiptOutboxRowAsync(CleansiaDbContext context) =>
        await context.OutboxMessages
            .IgnoreQueryFilters()
            .FirstAsync(m => m.QueueName == QueueNames.GenerateReceipt
                          && m.MessageKey == MessageKeys.Receipt(_orderId));

    private static async Task<int> OrderConfirmedPushOutboxCountAsync(CleansiaDbContext context)
    {
        var pushKey = MessageKeys.Push(_userId, NotificationEventCatalog.OrderConfirmed, _orderId);
        return await context.OutboxMessages
            .IgnoreQueryFilters()
            .CountAsync(m => m.QueueName == QueueNames.NotificationsDispatch && m.MessageKey == pushKey);
    }

    private static async Task SeedPendingCardOrder(CleansiaDbContext context)
    {
        // Single-tenant (TenantId == null) — the production web Checkout path. The order webhook is
        // anonymous (no tenant claim); the order-exists VALIDATOR rule (BaseRepository.ExistsAsync) is
        // tenant-scoped, so it resolves the order only when the row is single-tenant. The handler's own
        // read is tenant-ignoring; the effect rows then carry whatever tenant the order had (null here).
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        context.Currencies.Add(currency);

        var user = User.CreateWithPassword("order-webhook@cleansia.test", "12345678Test!", "Web", "Hook");
        user.ConfirmEmail();
        user.Created(Cleansia.TestUtilities.Constants.TestUserSession.TestUserId, DateTime.UtcNow);
        context.Users.Add(user);

        var order = Order.Create(
            customerName: "Web Hook",
            customerEmail: "order-webhook@cleansia.test",
            customerPhone: "+420777111222",
            customerAddress: Address.Create("Webhook St 1", "Brno", "60200", CountryId),
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1500m,
            currencyId: CurrencyId,
            paymentStatus: PaymentStatus.Pending,
            userId: user.Id);
        order.AssignStripeSessionId("cs_test_session");
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Pending, order));
        context.Add(order);

        await context.CommitAsync(CancellationToken.None);

        _orderId = order.Id;
        _userId = user.Id;
    }
}
