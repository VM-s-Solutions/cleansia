using System.Linq.Expressions;
using Cleansia.Core.Domain.Packages;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class PackageSpecification : ISpecification<Package>
{
    public string? SearchTerm { get; set; }

    public bool? IsActive { get; set; }

    public Expression<Func<Package, bool>> SatisfiedBy()
    {
        Specification<Package> specification = new TrueSpecification<Package>();

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<Package>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<Package>(x =>
                x.Name.ToLower().Contains(searchLower) ||
                x.Description.ToLower().Contains(searchLower)
            );
        }

        return specification.SatisfiedBy();
    }

    public static PackageSpecification Create(
        string? searchTerm = null,
        bool? isActive = null) =>
        new()
        {
            SearchTerm = searchTerm,
            IsActive = isActive
        };
}
