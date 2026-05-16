using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

public class SetRecurringBookingActive
{
    public record Command(string TemplateId, bool IsActive) : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        private readonly IRecurringBookingTemplateRepository _templateRepository;
        private readonly IUserSessionProvider _userSessionProvider;

        public Validator(
            IRecurringBookingTemplateRepository templateRepository,
            IUserSessionProvider userSessionProvider)
        {
            _templateRepository = templateRepository;
            _userSessionProvider = userSessionProvider;

            RuleFor(x => x.TemplateId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(ExistsAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotFound)
                .MustAsync(BeOwnedByCallerAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotOwnedByUser);
        }

        private async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
        {
            return await _templateRepository.GetByIdAsync(id, cancellationToken) != null;
        }

        private async Task<bool> BeOwnedByCallerAsync(string id, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId)) return false;
            var template = await _templateRepository.GetByIdAsync(id, cancellationToken);
            return template != null && template.UserId == userId;
        }
    }

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = (await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken))!;
            if (command.IsActive) template.Resume(); else template.Pause();
            return BusinessResult.Success();
        }
    }
}
