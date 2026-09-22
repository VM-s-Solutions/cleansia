using Cleansia.Core.AppServices.Features.Legal;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Legal;

namespace Cleansia.Core.AppServices.Mappers;

public static class WorkContractMappers
{
    /// <summary>
    /// The contract text rendered for a job: <c>{{currency}}</c> is the ORDER's currency, read off the
    /// facts, never the market's default — the job is priced in exactly one.
    /// </summary>
    public static WorkContractDto MapToWorkContractDto(
        this LegalDocument document,
        LegalDocumentText text,
        WorkContractFacts facts,
        WorkContractAcceptanceDetails? acceptance)
    {
        var placeholders = new Dictionary<string, string>
        {
            [LegalMarkdownRenderer.CurrencyPlaceholder] = facts.CurrencyCode,
        };

        return new WorkContractDto(
            LegalDocumentTextId: text.Id,
            LegalDocumentId: document.Id,
            Version: document.Version,
            EffectiveFrom: document.EffectiveFrom,
            Language: text.Language,
            Title: text.Title,
            ContentHtml: LegalMarkdownRenderer.Render(text.ContentMarkdown, placeholders),
            Facts: facts,
            Acceptance: acceptance);
    }

    public static WorkContractAcceptanceDto MapToDto(this WorkContractAcceptanceRow row) =>
        new(
            Id: row.Id,
            OrderEmployeeId: row.OrderEmployeeId,
            EmployeeId: row.EmployeeId,
            AcceptedOn: row.AcceptedOn,
            DocumentVersion: row.DocumentVersion,
            Language: row.Language);
}
