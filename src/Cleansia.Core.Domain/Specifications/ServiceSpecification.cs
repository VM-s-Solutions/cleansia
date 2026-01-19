using System.Linq.Expressions;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class ServiceSpecification : BaseSpecification<string?>, ISpecification<Service>
{
    public string? SearchTerm { get; set; }

    public Expression<Func<Service, bool>> SatisfiedBy()
    {
        Specification<Service> specification = new TrueSpecification<Service>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<Service>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<Service>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<Service>(x =>
                x.Name.ToLower().Contains(searchLower) ||
                x.Description.ToLower().Contains(searchLower)
            );
        }

        return specification.SatisfiedBy();
    }

    public static ServiceSpecification Create(
        string? id = null,
        bool? isActive = null,
        string? searchTerm = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            SearchTerm = searchTerm
        };
}