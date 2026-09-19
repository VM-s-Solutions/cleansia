using System.Security.Claims;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

[AuditAction("customer.consent.grant", Audience = AuditAudience.Customer, ResourceType = "User")]
public static class GrantConsent
{
    // IpAddress + UserAgent were previously on the Command but the client
    // could lie about them. They're now read server-side from
    // IRequestMetadataProvider so the legal-audit fields can't be spoofed.
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
        IConsentService consentService,
        ILegalDocumentResolver legalDocumentResolver,
        IAuditContext auditContext)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;

            // The same command is routed on both Partner hosts, and an employee accepts a different
            // document than the customer texts (ADR-0041) — their row stays unversioned. A signed-in
            // customer names no market, so the default market's text is the one in force for them.
            var isCustomer = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value == UserProfile.Customer.ToString();
            var document = isCustomer && LegalDocument.TypeFor(request.ConsentType) is { } documentType
                ? await legalDocumentResolver.ResolveInForceAsync(documentType, countryId: null, cancellationToken)
                : null;

            var granted = await consentService.TryGrantAsync(userId, request.ConsentType, document, cancellationToken);

            if (!granted)
            {
                return BusinessResult.Failure(new Error(
                    nameof(Command.ConsentType), BusinessErrorMessage.ConsentAlreadyGranted));
            }

            auditContext.RecordEvidence("User", userId, new ConsentEvidence(request.ConsentType, document?.Version));

            return BusinessResult.Success();
        }
    }
}
