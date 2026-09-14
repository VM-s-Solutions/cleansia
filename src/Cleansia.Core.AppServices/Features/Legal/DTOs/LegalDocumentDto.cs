using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Features.Legal.DTOs;

public record LegalDocumentDto(
    LegalDocumentType Type,
    string? CountryId,
    string Version,
    DateOnly EffectiveFrom,
    string Language,
    string Title,
    string ContentHtml,
    string ContentHash);
