using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0037 D1 — offerability is a two-axis predicate: the fulfilment axis says the work is not
/// OVER, the money axis says no scheduled sweep can still retract the order out from under the
/// cleaner who takes it. These pin the in-memory form; <c>OrderAvailabilityEquivalenceTests</c>
/// (real Postgres) pins that the queryable form answers identically.
/// </summary>
public class OrderAvailabilityTests
{
    private const string RecurringTemplateId = "tpl-weekly-1";
    private const bool Confirmed = true;

    [Theory]
    // New is admissible only for the money model that expects payment AT the job, and only when no
    // sweep can retract it. A one-off cash order matches no retractor: the take IS the confirmation.
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, null, false, true)]
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Paid, null, false, true)]
    // AutoCancelStaleRecurringOrders cancels an unconfirmed recurring occurrence at T-1h, so a
    // recurring cash order the customer has not confirmed is NOT retraction-free.
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, false, false)]
    // THE ROW THE 2026-09-28 RULING TURNS ON. The customer's confirmation is its own marker and the
    // occurrence stays Pending until the cleaner records the cash; if this is false, every confirmed
    // recurring cash occurrence is invisible on every board.
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, Confirmed, true)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, Confirmed, true)]
    // The marker admits cash only: a card occurrence is confirmed by its payment, not by the tap.
    [InlineData(OrderStatus.New, PaymentType.Card, PaymentStatus.Pending, RecurringTemplateId, Confirmed, false)]
    [InlineData(OrderStatus.New, PaymentType.Card, PaymentStatus.Paid, RecurringTemplateId, Confirmed, true)]
    // An occurrence confirmed before the ruling carries Paid, and stays offerable.
    [InlineData(OrderStatus.Confirmed, PaymentType.Cash, PaymentStatus.Paid, RecurringTemplateId, false, true)]
    // Checkout open or abandoned: CleanupStalePendingOrders cancels it within ~1h15m.
    [InlineData(OrderStatus.New, PaymentType.Card, PaymentStatus.Pending, null, false, false)]
    // THE ROW THE RULING TURNS ON (T-0691). A paid card order rests at New, because Confirmed now
    // means only "a cleaner took this job" and the webhook no longer writes it. If this is false, every
    // paid card job is invisible on every board, no cleaner can ever take one, and CancelUnfilledOrders
    // eventually refunds the customer who paid — an outage that reports itself as silence.
    [InlineData(OrderStatus.New, PaymentType.Card, PaymentStatus.Paid, null, false, true)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, null, false, true)]
    // Reachable two ways (admin override with no payment guard; a decline deliberately left Pending
    // for retry) and the 15-minute sweep has no OrderStatus term, so it kills this one out from
    // under an already-assigned cleaner.
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Pending, null, false, false)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Failed, null, false, false)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Cash, PaymentStatus.Pending, null, false, true)]
    // Pending is a dead status (ADR-0037 D5) and never offerable, whatever the money axis says.
    [InlineData(OrderStatus.Pending, PaymentType.Cash, PaymentStatus.Paid, null, false, false)]
    [InlineData(OrderStatus.Pending, PaymentType.Card, PaymentStatus.Paid, null, false, false)]
    // Work has begun but the job is NOT over: a half-crewed order stays offerable so its empty seat
    // can still be filled (owner ruling 2026-09-06). One cleaner tapping "on my way" writes an
    // ORDER-level status, and while that meant "not offerable" it took the whole job off every board
    // with the other seat still empty. Every consumer conjoins the free-seat term for itself, so this
    // can never offer a FULL order.
    [InlineData(OrderStatus.OnTheWay, PaymentType.Card, PaymentStatus.Paid, null, false, true)]
    [InlineData(OrderStatus.InProgress, PaymentType.Card, PaymentStatus.Paid, null, false, true)]
    // The money axis still bites after the work starts — the widened status axis must not swallow it.
    [InlineData(OrderStatus.OnTheWay, PaymentType.Card, PaymentStatus.Pending, null, false, false)]
    [InlineData(OrderStatus.InProgress, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, false, false)]
    // The order is over.
    [InlineData(OrderStatus.Completed, PaymentType.Card, PaymentStatus.Paid, null, false, false)]
    [InlineData(OrderStatus.Cancelled, PaymentType.Card, PaymentStatus.Paid, null, false, false)]
    [InlineData(OrderStatus.Cancelled, PaymentType.Cash, PaymentStatus.Pending, null, false, false)]
    [InlineData(OrderStatus.Cancelled, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, Confirmed, false)]
    public void IsOfferable_Answers_The_Two_Axis_Predicate(
        OrderStatus status,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId,
        bool customerConfirmed,
        bool expected)
    {
        DateTime? customerConfirmedAt = customerConfirmed ? DateTime.UtcNow : null;
        Assert.Equal(expected, OrderAvailability.IsOfferable(
            status, paymentType, paymentStatus, recurringTemplateId, customerConfirmedAt));
    }

    [Fact]
    public void The_Coarse_Client_Floor_Is_The_Statuses_The_Rule_Can_Ever_Admit()
    {
        Assert.Equal(
            new[]
            {
                OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay, OrderStatus.InProgress,
            },
            OrderAvailability.OfferableStatuses);

        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            var everOfferable = Enum.GetValues<PaymentType>()
                .SelectMany(_ => Enum.GetValues<PaymentStatus>(), (type, paymentStatus) => (type, paymentStatus))
                .Any(pair => OrderAvailability.IsOfferable(status, pair.type, pair.paymentStatus, null, null)
                    || OrderAvailability.IsOfferable(status, pair.type, pair.paymentStatus, RecurringTemplateId, DateTime.UtcNow));

            Assert.Equal(OrderAvailability.OfferableStatuses.Contains(status), everOfferable);
        }
    }

    /// <summary>
    /// ADR-0037 D3 extension obligation. The rule fails SAFE on a new <see cref="PaymentType"/> (an
    /// unknown type is not offerable at New) but it fails SILENTLY, and wrongly for a pay-on-site
    /// type such as Invoice, which is semantically cash on both axes. This goes red the moment a
    /// member is added and stays red until it is classified here AND in
    /// <see cref="OrderAvailability"/> — on the status axis (offerable at New?) and on the money
    /// axis (which sweep can retract it?).
    /// </summary>
    [Fact]
    public void Every_PaymentType_Is_Classified_On_Both_Axes()
    {
        var offerableAtNew = new Dictionary<PaymentType, bool>
        {
            [PaymentType.Cash] = true,
            [PaymentType.Card] = false,
        };

        var retractionFreeWhenUnpaidAndOneOff = new Dictionary<PaymentType, bool>
        {
            [PaymentType.Cash] = true,
            [PaymentType.Card] = false,
        };

        foreach (var paymentType in Enum.GetValues<PaymentType>())
        {
            Assert.True(
                offerableAtNew.ContainsKey(paymentType) && retractionFreeWhenUnpaidAndOneOff.ContainsKey(paymentType),
                $"PaymentType.{paymentType} is unclassified: decide whether it is offerable at New and " +
                "which scheduled sweep can retract it, in OrderAvailability and here.");

            Assert.Equal(
                offerableAtNew[paymentType],
                OrderAvailability.IsOfferable(OrderStatus.New, paymentType, PaymentStatus.Pending, null, null));

            Assert.Equal(
                retractionFreeWhenUnpaidAndOneOff[paymentType],
                OrderAvailability.IsOfferable(OrderStatus.Confirmed, paymentType, PaymentStatus.Pending, null, null));
        }
    }
}
