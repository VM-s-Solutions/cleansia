using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

[AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ResourceType = "Order", AllowsAnonymousActor = true)]
public class CancelGuestOrder
{
    public record Command(string DisplayOrderNumber, string Email, string ConfirmationCode,
        string? Reason = null, string Language = Constants.Language.English)
        : ICommand<CancelOrder.Response>, IGuestOrderScopedRequest
    {
        string? IOperatorScopedRequest.CountryId => null;
    }

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ILanguageRepository languageRepository)
        {
            RuleFor(x => x.DisplayOrderNumber).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.Email).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.ConfirmationCode).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.Reason).MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
            RuleFor(x => x.Language).SetValidator(new LanguageValidator(languageRepository));
        }
    }

    public class Handler(GuestOrderAccess guestOrderAccess,
        CustomerOrderCancellation cancellation, IPendingDispatch pending)
        : ICommandHandler<Command, CancelOrder.Response>
    {
        public async Task<BusinessResult<CancelOrder.Response>> Handle(
            Command command, CancellationToken cancellationToken)
        {
            var order = await guestOrderAccess.OrdersForKey(command)
                .Include(o => o.Currency)
                .Include(o => o.OrderStatusHistory)
                .Include(o => o.AssignedEmployees).ThenInclude(a => a.Employee)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);
            if (order is null)
            {
                return BusinessResult.Failure<CancelOrder.Response>(
                    new Error(nameof(command.DisplayOrderNumber), BusinessErrorMessage.OrderNotFound));
            }
            if (CancellationAssessor.BlockedReason(order) is { } blockedReason)
            {
                return BusinessResult.Failure<CancelOrder.Response>(
                    new Error(nameof(command.DisplayOrderNumber), blockedReason));
            }

            var outcome = await cancellation.ExecuteAsync(order, command.Reason, "System", cancellationToken);
            var key = MessageKeys.GuestOrderCancelledEmail(order.Id);
            pending.Enqueue(QueueNames.SendEmail,
                new QueueEnvelope<SendGuestOrderCancellationEmailMessage>(key, order.TenantId,
                    new SendGuestOrderCancellationEmailMessage(order.Id, command.Language,
                        outcome.SuccessfulRefundAmount, order.TenantId)), key);
            return BusinessResult.Success(outcome.Response);
        }
    }
}
