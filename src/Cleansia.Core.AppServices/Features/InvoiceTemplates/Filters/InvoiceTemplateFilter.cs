namespace Cleansia.Core.AppServices.Features.InvoiceTemplates.Filters;

public class InvoiceTemplateFilter
{
    public string? SearchTerm { get; init; }
    public string? CountryId { get; init; }
    public string? LanguageId { get; init; }
    public bool? IsActive { get; init; }
}