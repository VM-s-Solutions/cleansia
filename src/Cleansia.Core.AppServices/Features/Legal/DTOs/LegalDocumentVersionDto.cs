using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Features.Legal.DTOs;

public record LegalDocumentVersionDto(
    string Id,
    LegalDocumentAudience Audience,
    LegalDocumentType Type,
    string? CountryId,
    string? CountryIsoCode,
    DateOnly EffectiveFrom,
    string Version,
    bool IsInForce,
    string? Notes,
    IReadOnlyList<LegalDocumentTextSummaryDto> Texts);

public record LegalDocumentTextSummaryDto(
    string Language,
    string Title,
    string ContentHash);
