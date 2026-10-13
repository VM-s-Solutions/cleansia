using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Bookings;

public class SendRecurringOrderReminder
{
    public record Command(string OrderId, DateTime NowUtc, DateTime WindowStartUtc, DateTime WindowEndUtc) : ICommand<Response>;
    public record Response(bool Sent);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.OrderId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.WindowEndUtc).GreaterThan(x => x.WindowStartUtc);
        }
    }

    public class Handler(IOrderRepository orderRepository, INotificationProducer notificationProducer,
        ITenantProvider tenantProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var order = await orderRepository.GetQueryableIgnoringTenant()
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, cancellationToken);
            if (order is null || order.RecurringTemplateId is null || !order.AwaitsCustomerConfirmation
                || order.RecurringReminderSentAt is not null || string.IsNullOrEmpty(order.UserId)
                || order.CleaningDateTime < command.WindowStartUtc || order.CleaningDateTime > command.WindowEndUtc)
                return BusinessResult.Success(new Response(false));

            tenantProvider.ClearTenantOverride();
            tenantProvider.SetTenantOverride(order.TenantId!);
            await notificationProducer.NotifyAsync(order.UserId, NotificationEventCatalog.RecurringScheduled,
                new Dictionary<string, string> { ["orderId"] = order.Id, ["orderNumber"] = order.DisplayOrderNumber },
                order.TenantId, order.Id, cancellationToken);
            order.MarkRecurringReminderSent(command.NowUtc);
            return BusinessResult.Success(new Response(true));
        }
    }
}
