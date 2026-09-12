using System.Linq.Expressions;
using Cleansia.Core.Domain.Orders;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class ExtraSpecification : ISpecification<Extra>
{
    public string? SearchTerm { get; set; }

    public bool? IsActive { get; set; }

    public Expression<Func<Extra, bool>> SatisfiedBy()
    {
        Specification<Extra> specification = new TrueSpecification<Extra>();

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<Extra>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<Extra>(x =>
                x.Slug.Contains(searchLower) ||
                x.Name.ToLower().Contains(searchLower) ||
                (x.Description != null && x.Description.ToLower().Contains(searchLower)));
        }

        return specification.SatisfiedBy();
    }

    public static ExtraSpecification Create(string? searchTerm = null, bool? isActive = null) =>
        new() { SearchTerm = searchTerm, IsActive = isActive };
}
