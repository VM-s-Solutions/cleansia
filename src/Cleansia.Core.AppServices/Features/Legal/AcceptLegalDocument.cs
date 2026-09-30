using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Legal;

/// <summary>
/// A cleaner accepts one of their documents by echoing the id of the text row they were shown, which
/// must be a text of the document in force for their market today. Their consent row of that type moves
/// to it, and an acceptance row keeps the act — text, version, instant, IP and device — after the next
/// version moves the consent row on. Accepting the version already held is a success that writes nothing.
/// </summary>
public class AcceptLegalDocument
{
    public record Command(string AcceptedTextId) : ICommand<Response>;

    public record Response(LegalDocumentType Type, string Version);

    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserSessionProvider _userSessionProvider;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly ILegalDocumentRepository _legalDocumentRepository;
        private readonly ILegalDocumentResolver _legalDocumentResolver;

        public Validator(
            IUserSessionProvider userSessionProvider,
            IEmployeeRepository employeeRepository,
            ILegalDocumentRepository legalDocumentRepository,
            ILegalDocumentResolver legalDocumentResolver)
        {
            _userSessionProvider = userSessionProvider;
            _employeeRepository = employeeRepository;
            _legalDocumentRepository = legalDocumentRepository;
            _legalDocumentResolver = legalDocumentResolver;

            RuleFor(x => x.AcceptedTextId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MaximumLength(26)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(NamesADocumentInForceForTheCallerAsync)
                .WithMessage(BusinessErrorMessage.LegalDocumentNotInForce);
        }

        private async Task<bool> NamesADocumentInForceForTheCallerAsync(string textId, CancellationToken cancellationToken)
        {
            var document = await _legalDocumentRepository.GetByTextIdWithTextsAsync(textId, cancellationToken);
            if (document is null
                || document.Audience != LegalDocumentAudience.Employee
                || LegalDocument.CleanerConsentTypeFor(document.Type) is null)
            {
                return false;
            }

            var market = await CleanerLegalDocuments.MarketOfAsync(
                _employeeRepository, _userSessionProvider.GetUserId(), cancellationToken);
            if (market is null)
            {
                return false;
            }

            var inForce = await _legalDocumentResolver.ResolveInForceAsync(
                LegalDocumentAudience.Employee, document.Type, market.Value.CountryId, cancellationToken);
            return inForce?.Id == document.Id;
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IEmployeeRepository employeeRepository,
        ILegalDocumentRepository legalDocumentRepository,
        IConsentService consentService,
        ICleanerLegalDocumentAcceptanceRepository acceptanceRepository,
        IRequestMetadataProvider requestMetadataProvider,
        IHostAudienceProvider hostAudienceProvider) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var document = await legalDocumentRepository.GetByTextIdWithTextsAsync(command.AcceptedTextId, cancellationToken);
            if (document is null || LegalDocument.CleanerConsentTypeFor(document.Type) is not { } consentType)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.AcceptedTextId), BusinessErrorMessage.LegalDocumentNotInForce));
            }

            if (await consentService.TryGrantAsync(userId, consentType, document, cancellationToken))
            {
                var market = await CleanerLegalDocuments.MarketOfAsync(employeeRepository, userId, cancellationToken);
                acceptanceRepository.Add(CleanerLegalDocumentAcceptance.Create(
                    employeeId: market!.Value.EmployeeId,
                    text: document.Texts.First(t => t.Id == command.AcceptedTextId),
                    documentVersion: document.Version,
                    clientAudience: hostAudienceProvider.Audience,
                    ipAddress: requestMetadataProvider.IpAddress,
                    deviceLabel: requestMetadataProvider.DeviceLabel,
                    // The session's signed claim or nothing — never the X-Device-Id header, which is the
                    // client's word alone on a signed-in act.
                    deviceId: userSessionProvider.GetTypedUserClaim(AuthExtensions.DeviceIdClaimType)?.Value));
            }

            return BusinessResult.Success(new Response(document.Type, document.Version));
        }
    }
}
