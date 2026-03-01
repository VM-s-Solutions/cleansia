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
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IUserConsentRepository userConsentRepository)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var email = userSessionProvider.GetUserEmail();
            var user = await userRepository.GetByEmailAsync(email!, cancellationToken);

            if (user is null)
                return BusinessResult.Failure(new Error(
                    BusinessErrorMessage.NotExistingUserWithEmail, "User not found"));

            var consent = await userConsentRepository.GetByUserAndTypeAsync(
                user.Id, request.ConsentType, cancellationToken);

            if (consent is null)
                return BusinessResult.Failure(new Error(
                    BusinessErrorMessage.ConsentNotFound, "Consent not found"));

            consent.Withdraw();

            return BusinessResult.Success();
        }
    }
}
