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

    internal class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.ConsentType).IsInEnum();
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IConsentService consentService,
        IAuditContext auditContext)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;

            // The same command is routed on both Partner hosts, and an employee accepts a different
            // document than the customer constants describe (ADR-0041) — their row stays unversioned.
            var isCustomer = userSessionProvider.GetTypedUserClaim(ClaimTypes.Role)?.Value == UserProfile.Customer.ToString();
            var documentVersion = isCustomer ? LegalDocumentVersions.For(request.ConsentType) : null;

            var granted = await consentService.TryGrantAsync(userId, request.ConsentType, documentVersion, cancellationToken);

            if (!granted)
            {
                return BusinessResult.Failure(new Error(
                    nameof(Command.ConsentType), BusinessErrorMessage.ConsentAlreadyGranted));
            }

            auditContext.RecordEvidence("User", userId, new ConsentEvidence(request.ConsentType, documentVersion));

            return BusinessResult.Success();
        }
    }
}
