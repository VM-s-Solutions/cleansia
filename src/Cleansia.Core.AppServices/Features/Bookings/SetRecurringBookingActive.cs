using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

[AuditAction("customer.recurring.set_active", Audience = AuditAudience.Customer, ResourceType = "RecurringBookingTemplate",
    ResourceIdProperty = nameof(SetRecurringBookingActive.Command.TemplateId))]
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
                .MustAsync(_templateRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotFound)
                .MustAsync(BeOwnedByCallerAsync)
                .WithMessage(BusinessErrorMessage.RecurringTemplateNotOwnedByUser);
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
        IRecurringBookingTemplateRepository templateRepository,
        IAuditContext auditContext) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = (await templateRepository.GetByIdAsync(command.TemplateId, cancellationToken))!;
            var before = RecurringTemplateFacts.Of(template);
            if (command.IsActive)
            {
                template.Resume();
            }
            else
            {
                template.Pause();
            }

            auditContext.RecordEvidence("RecurringBookingTemplate", template.Id,
                new RecurringTemplateEvidence(before, RecurringTemplateFacts.Of(template)));

            return BusinessResult.Success();
        }
    }
}
