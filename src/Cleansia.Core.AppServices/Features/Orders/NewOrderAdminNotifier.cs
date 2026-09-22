using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The company's administrators are told of an order when it becomes OFFERABLE, never merely when
/// it is created — the same transition that tells the preferred cleaner, for the same reason: an
/// unpaid card order is an abandoned checkout the stale sweep cancels an hour later, and announcing
/// it hands the operator a job that vanishes. Shared by the three sites that make an order offerable
/// (a cash one-off at creation, a card order on its payment, a recurring occurrence on the customer's
/// confirm) so the event cannot drift between them. An order becomes offerable once, so its id is the
/// subject.
/// </summary>
public static class NewOrderAdminNotifier
{
    public static Task NotifyIfOfferableAsync(
        Order order,
        IAdminNotifier adminNotifier,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!OrderAvailability.IsOfferable(order.CurrentStatus, order.PaymentType, order.PaymentStatus, order.RecurringTemplateId))
        {
            return Task.CompletedTask;
        }

        if (order.TenantId is null)
        {
            // A market with no operator is a seed defect, not a reason to fail the booking.
            logger.LogWarning(
                "Order {OrderId} became offerable with no operating company, so no administrator can be told",
                order.Id);
            return Task.CompletedTask;
        }

        return adminNotifier.NotifyAsync(
            new AdminEvent(
                AdminNotificationEventCatalog.OrderNew,
                order.TenantId,
                Subject: order.Id,
                Args: new Dictionary<string, string>
                {
                    ["orderNumber"] = order.DisplayOrderNumber,
                    ["amount"] = MoneyText.Format(order.TotalPrice, order.Currency!),
                    ["paymentType"] = order.PaymentType.ToString(),
                    ["countryId"] = order.CustomerAddress!.CountryId,
                    ["orderId"] = order.Id,
                }),
            cancellationToken);
    }
}
