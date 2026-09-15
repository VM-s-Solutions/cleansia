using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Legal.DTOs;
using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Mappers;

public static class LegalDocumentMappers
{
    public static LegalDocumentDto MapToDto(this LegalDocument document, LegalDocumentText text, IReadOnlyDictionary<string, string> placeholders)
    {
        return new LegalDocumentDto(
            Type: document.Type,
            CountryId: document.CountryId,
            Version: document.Version,
            EffectiveFrom: document.EffectiveFrom,
            Language: text.Language,
            Title: text.Title,
            ContentHtml: LegalMarkdownRenderer.Render(text.ContentMarkdown, placeholders),
            ContentHash: text.ContentHash);
    }

    public static LegalDocumentVersionDto MapToVersionDto(this LegalDocument document, DateOnly today)
    {
        return new LegalDocumentVersionDto(
            Id: document.Id,
            Audience: document.Audience,
            Type: document.Type,
            CountryId: document.CountryId,
            CountryIsoCode: document.Country?.IsoCode,
            EffectiveFrom: document.EffectiveFrom,
            Version: document.Version,
            IsInForce: document.IsInForceOn(today),
            Notes: document.Notes,
            Texts: document.Texts
                .OrderBy(t => t.Language, StringComparer.Ordinal)
                .Select(t => new LegalDocumentTextSummaryDto(t.Language, t.Title, t.ContentHash))
                .ToList());
    }

    public static AdminLegalDocumentDto MapToAdminDto(this LegalDocument document, LegalDocumentText text, DateOnly today)
    {
        return new AdminLegalDocumentDto(
            Id: document.Id,
            Audience: document.Audience,
            Type: document.Type,
            CountryId: document.CountryId,
            CountryIsoCode: document.Country?.IsoCode,
            EffectiveFrom: document.EffectiveFrom,
            Version: document.Version,
            IsInForce: document.IsInForceOn(today),
            Notes: document.Notes,
            Language: text.Language,
            Title: text.Title,
            ContentMarkdown: text.ContentMarkdown,
            ContentHtml: LegalMarkdownRenderer.Render(text.ContentMarkdown),
            ContentHash: text.ContentHash);
    }
}
