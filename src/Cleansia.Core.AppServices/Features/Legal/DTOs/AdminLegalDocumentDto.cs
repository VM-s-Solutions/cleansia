using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Features.Legal.DTOs;

/// <summary>One language of one stored version, as seeded: the markdown itself, its rendering with the placeholders left visible, and the hash.</summary>
public record AdminLegalDocumentDto(
    string Id,
    LegalDocumentAudience Audience,
    LegalDocumentType Type,
    string? CountryId,
    string? CountryIsoCode,
    DateOnly EffectiveFrom,
    string Version,
    bool IsInForce,
    string? Notes,
    string Language,
    string Title,
    string ContentMarkdown,
    string ContentHtml,
    string ContentHash);
