using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Bookings;

[AuditAction("customer.recurring.delete", Audience = AuditAudience.Customer, ResourceType = "RecurringBookingTemplate",
    ResourceIdProperty = nameof(DeleteRecurringBooking.Command.TemplateId))]
public class DeleteRecurringBooking
{
    public record Command(string TemplateId) : ICommand;

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
            return await _templateRepository.GetByIdForOwnerAsync(id, _userSessionProvider.GetUserId() ?? string.Empty, cancellationToken) != null;
        }

        private async Task<bool> BeOwnedByCallerAsync(string id, CancellationToken cancellationToken)
        {
            var userId = _userSessionProvider.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var template = await _templateRepository.GetByIdForOwnerAsync(id, _userSessionProvider.GetUserId() ?? string.Empty, cancellationToken);
            return template != null && template.UserId == userId;
        }
    }

    public class Handler(
        IRecurringBookingTemplateRepository templateRepository,
        IUserSessionProvider userSessionProvider,
        IAuditContext auditContext) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command command, CancellationToken cancellationToken)
        {
            var template = (await templateRepository.GetByIdForOwnerAsync(command.TemplateId, userSessionProvider.GetUserId()!, cancellationToken))!;
            templateRepository.Remove(template);
            auditContext.RecordEvidence("RecurringBookingTemplate", template.Id,
                new RecurringTemplateEvidence(Before: RecurringTemplateFacts.Of(template), After: null));
            return BusinessResult.Success();
        }
    }
}
