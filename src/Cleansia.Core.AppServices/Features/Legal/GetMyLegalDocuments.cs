using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Legal;

/// <summary>
/// The cleaner's own documents in force for their market — the one they are approved for, and until then
/// the one their address is in — each with whether its current version is accepted. Empty while none is
/// seeded.
/// </summary>
public class GetMyLegalDocuments
{
    public record Query(string? Language = null) : IQuery<IReadOnlyList<CleanerLegalDocumentDto>>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Language).MaximumLength(10).WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IEmployeeRepository employeeRepository,
        ICountryConfigurationRepository countryConfigurationRepository,
        ILegalDocumentResolver legalDocumentResolver,
        IUserConsentRepository userConsentRepository,
        ICompanyInfoRepository companyInfoRepository)
        : IQueryHandler<Query, IReadOnlyList<CleanerLegalDocumentDto>>
    {
        public async Task<BusinessResult<IReadOnlyList<CleanerLegalDocumentDto>>> Handle(Query query, CancellationToken cancellationToken)
        {
            var userId = userSessionProvider.GetUserId()!;
            var countryId = (await CleanerLegalDocuments.MarketOfAsync(employeeRepository, userId, cancellationToken))?.CountryId;

            var documents = await CleanerLegalDocuments.InForceAsync(legalDocumentResolver, countryId, cancellationToken);
            if (documents.Count == 0)
            {
                return BusinessResult.Success<IReadOnlyList<CleanerLegalDocumentDto>>([]);
            }

            var market = countryId is null
                ? await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken)
                : await countryConfigurationRepository.GetByCountryIdAsync(countryId, cancellationToken);
            var company = market?.OperatorTenantId is { } operatorTenantId
                ? await companyInfoRepository.GetActiveForOperatorAsync(operatorTenantId, market.CountryId, cancellationToken)
                : null;
            var placeholders = LegalMarkdownRenderer.MarketPlaceholders(market, company);

            IReadOnlyList<UserConsent> consents = await userConsentRepository.GetByUserIdNoTrackingAsync(userId, cancellationToken);

            IReadOnlyList<CleanerLegalDocumentDto> items = documents
                .Select(document => (Document: document, Text: document.TextForOrFallback(query.Language)))
                .Where(entry => entry.Text is not null)
                .Select(entry => entry.Document.MapToCleanerDto(
                    entry.Text!, placeholders, CleanerLegalDocuments.AcceptanceOf(entry.Document, consents)))
                .ToList();

            return BusinessResult.Success(items);
        }
    }
}
