using System.Linq.Expressions;
using Cleansia.Core.Domain.Company;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class CompanyInfoSpecification : BaseSpecification<string?>, ISpecification<CompanyInfo>
{
    public string? SearchTerm { get; set; }
    public string? CountryId { get; set; }

    public Expression<Func<CompanyInfo, bool>> SatisfiedBy()
    {
        Specification<CompanyInfo> specification = new TrueSpecification<CompanyInfo>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<CompanyInfo>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<CompanyInfo>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(CountryId))
        {
            specification &= new DirectSpecification<CompanyInfo>(x => x.CountryId == CountryId);
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<CompanyInfo>(x =>
                x.LegalName.ToLower().Contains(searchLower) ||
                x.TradingName.ToLower().Contains(searchLower) ||
                x.City.ToLower().Contains(searchLower)
            );
        }

        return specification.SatisfiedBy();
    }

    public static CompanyInfoSpecification Create(
        string? id = null,
        bool? isActive = null,
        string? searchTerm = null,
        string? countryId = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            SearchTerm = searchTerm,
            CountryId = countryId
        };
}