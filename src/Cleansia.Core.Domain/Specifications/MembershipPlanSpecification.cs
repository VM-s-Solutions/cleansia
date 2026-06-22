using System.Linq.Expressions;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class MembershipPlanSpecification : BaseSpecification<string?>, ISpecification<MembershipPlan>
{
    public string? Search { get; set; }

    public Expression<Func<MembershipPlan, bool>> SatisfiedBy()
    {
        Specification<MembershipPlan> specification = new TrueSpecification<MembershipPlan>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<MembershipPlan>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<MembershipPlan>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var needle = Search.Trim().ToUpper();
            specification &= new DirectSpecification<MembershipPlan>(x =>
                x.Code.ToUpper().Contains(needle) ||
                x.Name.ToUpper().Contains(needle));
        }

        return specification.SatisfiedBy();
    }

    public static MembershipPlanSpecification Create(
        bool? isActive = null,
        string? search = null) =>
        new()
        {
            IsActive = isActive,
            Search = search
        };
}
