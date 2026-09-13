using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

[AuditAction("customer.consent.withdraw", Audience = AuditAudience.Customer, ResourceType = "User")]
public static class WithdrawConsent
{
    public record Command(ConsentType ConsentType) : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.ConsentType).IsInEnum().WithMessage(BusinessErrorMessage.InvalidEnumValue);
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IUserConsentRepository userConsentRepository,
        IAuditContext auditContext)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;
            var consent = await userConsentRepository.GetByUserAndTypeAsync(
                userId, request.ConsentType, cancellationToken);

            if (consent is null)
                return BusinessResult.Failure(new Error(
                    nameof(Command.ConsentType), BusinessErrorMessage.ConsentNotFound));

            consent.Withdraw();

            auditContext.RecordEvidence("User", userId, new ConsentEvidence(request.ConsentType, consent.DocumentVersion));

            return BusinessResult.Success();
        }
    }
}
