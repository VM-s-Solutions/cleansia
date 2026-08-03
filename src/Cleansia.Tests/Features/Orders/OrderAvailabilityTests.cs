using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0037 D1 — offerability is a two-axis predicate: the fulfilment axis says the work has not
/// started, the money axis says no scheduled sweep can still retract the order out from under the
/// cleaner who takes it. These pin the in-memory form; <c>OrderAvailabilityEquivalenceTests</c>
/// (real Postgres) pins that the queryable form answers identically.
/// </summary>
public class OrderAvailabilityTests
{
    private const string RecurringTemplateId = "tpl-weekly-1";

    [Theory]
    // New is admissible only for the money model that expects payment AT the job, and only when no
    // sweep can retract it. A one-off cash order matches no retractor: the take IS the confirmation.
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, null, true)]
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Paid, null, true)]
    // AutoCancelStaleRecurringOrders cancels an unconfirmed recurring occurrence at T-1h with no
    // PaymentType term, so a recurring cash order at New is NOT retraction-free.
    [InlineData(OrderStatus.New, PaymentType.Cash, PaymentStatus.Pending, RecurringTemplateId, false)]
    // A confirmed recurring occurrence (cash included) carries PaymentStatus.Paid.
    [InlineData(OrderStatus.Confirmed, PaymentType.Cash, PaymentStatus.Paid, RecurringTemplateId, true)]
    // Checkout open or abandoned: CleanupStalePendingOrders cancels it within ~1h15m.
    [InlineData(OrderStatus.New, PaymentType.Card, PaymentStatus.Pending, null, false)]
    [InlineData(OrderStatus.New, PaymentType.Card, PaymentStatus.Paid, null, false)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Paid, null, true)]
    // Reachable two ways (admin override with no payment guard; a decline deliberately left Pending
    // for retry) and the 15-minute sweep has no OrderStatus term, so it kills this one out from
    // under an already-assigned cleaner.
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Pending, null, false)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Card, PaymentStatus.Failed, null, false)]
    [InlineData(OrderStatus.Confirmed, PaymentType.Cash, PaymentStatus.Pending, null, true)]
    // Pending is a dead status (ADR-0037 D5) and never offerable, whatever the money axis says.
    [InlineData(OrderStatus.Pending, PaymentType.Cash, PaymentStatus.Paid, null, false)]
    [InlineData(OrderStatus.Pending, PaymentType.Card, PaymentStatus.Paid, null, false)]
    // Work has begun, or the order is over.
    [InlineData(OrderStatus.OnTheWay, PaymentType.Card, PaymentStatus.Paid, null, false)]
    [InlineData(OrderStatus.InProgress, PaymentType.Card, PaymentStatus.Paid, null, false)]
    [InlineData(OrderStatus.Completed, PaymentType.Card, PaymentStatus.Paid, null, false)]
    [InlineData(OrderStatus.Cancelled, PaymentType.Card, PaymentStatus.Paid, null, false)]
    [InlineData(OrderStatus.Cancelled, PaymentType.Cash, PaymentStatus.Pending, null, false)]
    public void IsOfferable_Answers_The_Two_Axis_Predicate(
        OrderStatus status,
        PaymentType paymentType,
        PaymentStatus paymentStatus,
        string? recurringTemplateId,
        bool expected)
    {
        Assert.Equal(expected, OrderAvailability.IsOfferable(status, paymentType, paymentStatus, recurringTemplateId));
    }

    [Fact]
    public void IsOfferable_Is_Total_Over_A_Null_Status()
    {
        Assert.False(OrderAvailability.IsOfferable(null, PaymentType.Cash, PaymentStatus.Paid, null));
        Assert.False(OrderAvailability.IsOfferable(null, PaymentType.Card, PaymentStatus.Paid, null));
    }

    [Fact]
    public void The_Coarse_Client_Floor_Is_The_Statuses_The_Rule_Can_Ever_Admit()
    {
        Assert.Equal(
            new[] { OrderStatus.New, OrderStatus.Confirmed },
            OrderAvailability.OfferableStatuses);

        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            var everOfferable = Enum.GetValues<PaymentType>()
                .SelectMany(_ => Enum.GetValues<PaymentStatus>(), (type, paymentStatus) => (type, paymentStatus))
                .Any(pair => OrderAvailability.IsOfferable(status, pair.type, pair.paymentStatus, null)
                    || OrderAvailability.IsOfferable(status, pair.type, pair.paymentStatus, RecurringTemplateId));

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
                OrderAvailability.IsOfferable(OrderStatus.New, paymentType, PaymentStatus.Pending, null));

            Assert.Equal(
                retractionFreeWhenUnpaidAndOneOff[paymentType],
                OrderAvailability.IsOfferable(OrderStatus.Confirmed, paymentType, PaymentStatus.Pending, null));
        }
    }
}
