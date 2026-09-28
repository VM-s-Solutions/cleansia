using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Features.Legal.DTOs;

/// <summary>
/// One document a cleaner must accept, in force for their market: the rendered text, the text-row id
/// the acceptance echoes, and what they last accepted. <see cref="IsAccepted"/> is false when the text
/// they accepted is not this one — an older version, or another market's.
/// </summary>
public record CleanerLegalDocumentDto(
    LegalDocumentType Type,
    string LegalDocumentId,
    string LegalDocumentTextId,
    string Version,
    DateOnly EffectiveFrom,
    string Language,
    string Title,
    string ContentHtml,
    string ContentHash,
    bool IsAccepted,
    string? AcceptedVersion,
    DateTimeOffset? AcceptedAt);
