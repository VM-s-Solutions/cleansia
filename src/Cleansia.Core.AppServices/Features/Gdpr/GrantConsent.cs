using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
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
        IConsentService consentService)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;

            var granted = await consentService.TryGrantAsync(userId, request.ConsentType, cancellationToken);

            return granted
                ? BusinessResult.Success()
                : BusinessResult.Failure(new Error(
                    BusinessErrorMessage.ConsentAlreadyGranted, "Consent already granted"));
        }
    }
}
