using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

/// <summary>
/// Pauses or resumes a recurring booking template. Pausing skips the
/// materializer (no new orders spawned) but preserves the template so the
/// user can resume later with the same configuration. Already-spawned
/// future Order rows are not cancelled — the user cancels those individually
/// via the regular order flow.
/// </summary>
public class SetRecurringBookingActive
{
    public record Command(string TemplateId, bool IsActive, string UserId = "") : ICommand;

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

            if (command.IsActive) template.Resume(); else template.Pause();
            return BusinessResult.Success();
        }
    }
}
