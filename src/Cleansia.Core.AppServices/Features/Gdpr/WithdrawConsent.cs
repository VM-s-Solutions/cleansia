using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class WithdrawConsent
{
    public record Command(ConsentType ConsentType) : ICommand;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.ConsentType).IsInEnum();
        }
    }

    internal class Handler(
        IUserSessionProvider userSessionProvider,
        IUserConsentRepository userConsentRepository)
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
                    BusinessErrorMessage.ConsentNotFound, "Consent not found"));

            consent.Withdraw();

            return BusinessResult.Success();
        }
    }
}
