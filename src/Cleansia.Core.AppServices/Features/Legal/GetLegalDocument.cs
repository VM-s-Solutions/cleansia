using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Legal;

/// <summary>
/// The customer legal text in force today for a market, rendered for the page: the named market's
/// own copy or the platform-wide one, the requested language or English, and the market's figures
/// filled in where the copy carries a placeholder — the currency prices are displayed in is the
/// market's, never written into a text (ADR-0060 D3). Anonymous: the register form and the booking
/// wizard link here before anyone signs in.
/// </summary>
public class GetLegalDocument
{
    public record Query(LegalDocumentType Type, string? CountryId = null, string? Language = null) : IQuery<LegalDocumentDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Type).IsInEnum().WithMessage(BusinessErrorMessage.InvalidEnumValue);
            RuleFor(x => x.CountryId).MaximumLength(26).WithMessage(BusinessErrorMessage.MaxLength);
            RuleFor(x => x.Language).MaximumLength(10).WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        ICountryConfigurationRepository countryConfigurationRepository,
        ILegalDocumentResolver legalDocumentResolver) : IQueryHandler<Query, LegalDocumentDto>
    {
        public async Task<BusinessResult<LegalDocumentDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var market = request.CountryId is null
                ? await countryConfigurationRepository.GetDefaultMarketAsync(cancellationToken)
                : await countryConfigurationRepository.GetByCountryIdAsync(request.CountryId, cancellationToken);
            if (market is null)
            {
                return BusinessResult.Failure<LegalDocumentDto>(
                    new Error(nameof(request.CountryId), BusinessErrorMessage.CountryNotServiced));
            }

            var document = await legalDocumentResolver.ResolveInForceAsync(request.Type, market.CountryId, cancellationToken);
            var text = document?.TextForOrFallback(request.Language);
            if (document is null || text is null)
            {
                return BusinessResult.Failure<LegalDocumentDto>(
                    new Error(nameof(request.Type), BusinessErrorMessage.LegalDocumentNotFound));
            }

            var placeholders = new Dictionary<string, string>
            {
                [LegalMarkdownRenderer.CurrencyPlaceholder] = market.DefaultCurrencyCode,
            };

            return BusinessResult.Success(document.MapToDto(text, placeholders));
        }
    }
}
