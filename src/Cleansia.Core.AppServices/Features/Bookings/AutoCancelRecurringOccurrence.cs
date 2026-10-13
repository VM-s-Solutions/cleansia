using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using BusinessResult = Cleansia.Infra.Common.Validations.BusinessResult;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Cancels one unconfirmed recurring occurrence for <see cref="AutoCancelStaleRecurringOrders"/>, which sends each
/// occurrence in a DI scope of its own. The pipeline commits it inside that scope and a failure leaves with the
/// scope, so this handler neither commits nor catches. → /decisions/adr-0072
/// </summary>
public class AutoCancelRecurringOccurrence
{
    /// <param name="CutoffUtc">The sweep's cutoff: an occurrence whose slot is later is left alone.</param>
    public record Command(string OrderId, DateTime CutoffUtc) : ICommand<Response>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId).NotEmpty().WithMessage(BusinessErrorMessage.Required);

            // Bare, like the sweep's own bound: a scheduler sends this, so a translated key would be read by nobody.
            RuleFor(x => x.CutoffUtc).Must(cutoff =>
                cutoff <= DateTime.UtcNow.AddHours(AutoCancelStaleRecurringOrders.MaxMissedConfirmGraceHours));
        }
    }

    public record Response(bool Cancelled);

    /// <summary>No JWT on a sweep. Matches ExpireStaleReferrals and LoyaltyService.</summary>
    private const string SystemActor = "system";

    public class Handler(
        IOrderRepository orderRepository,
        ICreditAccountRepository creditAccountRepository,
        INotificationProducer notificationProducer,
        ITenantProvider tenantProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetQueryableIgnoringTenant()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);

            // The sweep's selection is a snapshot: the customer may have confirmed, paid or cancelled since.
            if (order is null
                || !order.AwaitsCustomerConfirmation
                || order.CleaningDateTime > command.CutoffUtc
                || string.IsNullOrEmpty(order.UserId))
            {
                return BusinessResult.Success(new Response(false));
            }

            // The Cancelled track is stamped from the ambient tenant at commit, and a sweep has no JWT.
            tenantProvider.ClearTenantOverride();
            if (!string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
            }

            order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.Cancelled, order));

            // An unconfirmed occurrence was never charged, but credit applied when it was materialized comes back,
            // in the commit that cancels it, onto the customer's credit account, whose company comes from the
            // account and not from the override. → /flows/cross-cutting#tenancy
            await creditAccountRepository.ReturnUnpaidOrderCreditAsync(
                order, cardRefunded: 0m, SystemActor, cancellationToken);

            // Fee-free by this sweep's own rule, and now it says so on the order.
            order.Cancel(
                DateTime.UtcNow,
                CancelledBy.System,
                feeRate: 0m,
                refundAmount: 0m,
                reason: OrderCancellationReasons.RecurringNotConfirmed);

            await notificationProducer.NotifyAsync(
                order.UserId,
                NotificationEventCatalog.OrderCancelled,
                new Dictionary<string, string>
                {
                    ["orderId"] = order.Id,
                    ["orderNumber"] = order.DisplayOrderNumber,
                },
                order.TenantId,
                order.Id,
                cancellationToken);

            return BusinessResult.Success(new Response(true));
        }
    }
}
