using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.EmailTemplates.Filters;

public class EmailTemplateFilter
{
    public string? SearchTerm { get; init; }
    public EmailType? EmailType { get; init; }
    public string? LanguageId { get; init; }
}