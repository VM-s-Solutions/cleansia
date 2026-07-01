using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Users;

namespace Cleansia.TestUtilities.MockDataFactories.Orders;

public class OrderMockFactory
{
    public class OrderPartial
    {
        public string? Id { get; set; }

        public string? UserId { get; set; }

        public string? CustomerName { get; set; }

        public string? CustomerEmail { get; set; }

        public string? CustomerPhone { get; set; }

        public Address? CustomerAddress { get; set; }

        public int? Rooms { get; set; }

        public int? Bathrooms { get; set; }

        public DateTime? CleaningDateTime { get; set; }

        public PaymentType? PaymentType { get; set; }

        public decimal? TotalPrice { get; set; }

        public string? CurrencyId { get; set; }

        public PaymentStatus? PaymentStatus { get; set; }

        public string? StripeSessionId { get; set; }

        public string? TenantId { get; set; }

        public OrderStatus? CurrentStatus { get; set; }
    }

    public static Order Generate(OrderPartial? mergeFrom = null, Currency? currency = null)
    {
        var partial = mergeFrom ?? new OrderPartial();
        var resolvedCurrency = currency ?? Currency.Create("CZK", "Kč", "Czech Koruna", 1m);

        var order = Order.Create(
            customerName: partial.CustomerName ?? "Test Customer",
            customerEmail: partial.CustomerEmail ?? Constants.TestUserSession.TestUserEmail,
            customerPhone: partial.CustomerPhone ?? Constants.TestUserSession.TestUserPhone,
            customerAddress: partial.CustomerAddress!,
            rooms: partial.Rooms ?? 2,
            bathrooms: partial.Bathrooms ?? 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: partial.CleaningDateTime ?? DateTime.UtcNow.AddDays(5),
            paymentType: partial.PaymentType ?? PaymentType.Card,
            totalPrice: partial.TotalPrice ?? 1000m,
            currencyId: partial.CurrencyId ?? resolvedCurrency.Id,
            paymentStatus: partial.PaymentStatus ?? PaymentStatus.Paid,
            userId: partial.UserId ?? Constants.TestUserSession.TestUserId);
        order.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow);
        order.SetCurrency(resolvedCurrency);

        if (!string.IsNullOrEmpty(partial.Id))
        {
            order.Id = partial.Id;
        }

        if (!string.IsNullOrEmpty(partial.StripeSessionId))
        {
            order.AssignStripeSessionId(partial.StripeSessionId);
        }

        if (!string.IsNullOrEmpty(partial.TenantId))
        {
            order.TenantId = partial.TenantId;
        }

        order.AddOrderStatus(OrderStatusTrack.Create(partial.CurrentStatus ?? OrderStatus.New, order));

        return order;
    }

    /// <summary>
    /// Builds a card-paid order carrying a TIME-ORDERED status history, so a handler that resolves the
    /// "latest status" by <c>CreatedOn</c> descending gets a deterministic answer. Each track is stamped
    /// one minute after the previous one. Use this for cancellation/refund handler tests that need both an
    /// acceptance entry (<see cref="OrderStatus.Confirmed"/>) and a specific terminal latest status.
    /// </summary>
    public static Order GenerateWithStatusHistory(
        IReadOnlyList<OrderStatus> statuses,
        decimal totalPrice = 1000m,
        string orderId = "order-1",
        string userId = "user-1",
        PaymentType paymentType = PaymentType.Card,
        PaymentStatus paymentStatus = PaymentStatus.Paid,
        string? stripeSessionId = "cs_test_refund",
        string? stripePaymentIntentId = null,
        Currency? currency = null)
    {
        var resolvedCurrency = currency ?? Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var order = Order.Create(
            customerName: "Test Customer",
            customerEmail: Constants.TestUserSession.TestUserEmail,
            customerPhone: Constants.TestUserSession.TestUserPhone,
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(10),
            paymentType: paymentType,
            totalPrice: totalPrice,
            currencyId: resolvedCurrency.Id,
            paymentStatus: paymentStatus,
            userId: userId);
        order.Id = orderId;
        order.Created(Constants.TestUserSession.TestUserName, DateTime.UtcNow.AddDays(-1));
        order.SetCurrency(resolvedCurrency);

        if (!string.IsNullOrEmpty(stripeSessionId))
        {
            order.AssignStripeSessionId(stripeSessionId);
        }

        if (!string.IsNullOrEmpty(stripePaymentIntentId))
        {
            order.AssignStripePaymentIntentId(stripePaymentIntentId);
        }

        var stamp = DateTimeOffset.UtcNow.AddHours(-statuses.Count);
        foreach (var status in statuses)
        {
            var track = OrderStatusTrack.Create(status, order);
            track.Created(Constants.TestUserSession.TestUserName, stamp);
            order.AddOrderStatus(track);
            stamp = stamp.AddMinutes(1);
        }

        return order;
    }
}
