using System.Linq.Expressions;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class InvoiceTemplateSpecification : BaseSpecification<string?>, ISpecification<InvoiceTemplate>
{
    public string? SearchTerm { get; set; }
    public string? CountryId { get; set; }
    public string? LanguageId { get; set; }

    public Expression<Func<InvoiceTemplate, bool>> SatisfiedBy()
    {
        Specification<InvoiceTemplate> specification = new TrueSpecification<InvoiceTemplate>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<InvoiceTemplate>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<InvoiceTemplate>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(CountryId))
        {
            specification &= new DirectSpecification<InvoiceTemplate>(x => x.CountryId == CountryId);
        }

        if (!string.IsNullOrWhiteSpace(LanguageId))
        {
            specification &= new DirectSpecification<InvoiceTemplate>(x => x.LanguageId == LanguageId);
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<InvoiceTemplate>(x =>
                x.TemplateName.ToLower().Contains(searchLower) ||
                (x.Description != null && x.Description.ToLower().Contains(searchLower))
            );
        }

        return specification.SatisfiedBy();
    }

    public static InvoiceTemplateSpecification Create(
        string? id = null,
        bool? isActive = null,
        string? searchTerm = null,
        string? countryId = null,
        string? languageId = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            SearchTerm = searchTerm,
            CountryId = countryId,
            LanguageId = languageId
        };
}