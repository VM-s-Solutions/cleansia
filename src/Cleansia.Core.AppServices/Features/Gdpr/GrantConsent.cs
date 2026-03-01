using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class GrantConsent
{
    public record Command(ConsentType ConsentType, string? IpAddress, string? UserAgent) : ICommand;

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

            var existing = await userConsentRepository.GetByUserAndTypeAsync(
                user.Id, request.ConsentType, cancellationToken);

            if (existing is not null)
            {
                if (existing.IsGranted)
                    return BusinessResult.Failure(new Error(
                        BusinessErrorMessage.ConsentAlreadyGranted, "Consent already granted"));

                existing.Regrant(request.IpAddress, request.UserAgent);
            }
            else
            {
                var consent = UserConsent.Grant(user.Id, request.ConsentType, request.IpAddress, request.UserAgent);
                userConsentRepository.Add(consent);
            }

            return BusinessResult.Success();
        }
    }
}
