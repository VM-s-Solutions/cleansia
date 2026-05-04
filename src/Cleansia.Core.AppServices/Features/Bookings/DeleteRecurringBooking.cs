using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Hard-deletes a recurring booking template the calling user owns. Already
/// materialized future Order rows survive — the user cancels those one by
/// one through the regular order flow if they don't want them. For most
/// users "I don't want this anymore" maps to <see cref="SetRecurringBookingActive"/>
/// (Pause); Delete is the explicit "I'm sure, wipe it" path.
/// </summary>
public class DeleteRecurringBooking
{
    public record Command(string TemplateId, string UserId = "") : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
            RuleFor(x => x.TemplateId).NotEmpty().WithMessage(BusinessErrorMessage.Required);
        }
    }

    public class Handler(IRecurringBookingTemplateRepository templateRepository) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken);
            if (template == null)
            {
                return BusinessResult.Failure(new Error(
                    nameof(command.TemplateId),
                    BusinessErrorMessage.RecurringTemplateNotFound));
            }
            if (template.UserId != command.UserId)
            {
                return BusinessResult.Failure(new Error(
                    nameof(command.TemplateId),
                    BusinessErrorMessage.RecurringTemplateNotOwnedByUser));
            }

            templateRepository.Remove(template);
            return BusinessResult.Success();
        }
    }
}
