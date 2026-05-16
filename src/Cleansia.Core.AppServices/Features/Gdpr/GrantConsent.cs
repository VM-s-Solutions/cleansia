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
    // IpAddress + UserAgent were previously on the Command but the client
    // could lie about them. They're now read server-side from
    // IRequestMetadataProvider so the legal-audit fields can't be spoofed.
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
        IRequestMetadataProvider requestMetadata,
        IUserConsentRepository userConsentRepository)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;
            var ipAddress = requestMetadata.IpAddress;
            var userAgent = requestMetadata.DeviceLabel;

            var existing = await userConsentRepository.GetByUserAndTypeAsync(
                userId, request.ConsentType, cancellationToken);

            if (existing is not null)
            {
                if (existing.IsGranted)
                    return BusinessResult.Failure(new Error(
                        BusinessErrorMessage.ConsentAlreadyGranted, "Consent already granted"));

                existing.Regrant(ipAddress, userAgent);
            }
            else
            {
                var consent = UserConsent.Grant(userId, request.ConsentType, ipAddress, userAgent);
                userConsentRepository.Add(consent);
            }

            return BusinessResult.Success();
        }
    }
}
