namespace Cleansia.Core.AppServices.Features.ReceiptTemplates.Filters;

public class ReceiptTemplateFilter
{
    public string? SearchTerm { get; init; }
    public string? CountryId { get; init; }
    public string? LanguageId { get; init; }
    public bool? IsActive { get; init; }
}