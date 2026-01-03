using System.Linq.Expressions;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class EmployeeSpecification : BaseSpecification<string?>, ISpecification<Employee>
{
    public List<ContractStatus>? ContractStatuses { get; set; }

    public string? SearchTerm { get; set; }

    public Expression<Func<Employee, bool>> SatisfiedBy()
    {
        Specification<Employee> specification = new TrueSpecification<Employee>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<Employee>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<Employee>(x => x.User!.IsActive == IsActive.Value);
        }

        if (ContractStatuses != null && ContractStatuses.Count > 0)
        {
            specification &= new DirectSpecification<Employee>(x => ContractStatuses.Contains(x.ContractStatus));
        }

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<Employee>(x =>
                x.User!.FirstName.ToLower().Contains(searchLower) ||
                x.User!.LastName.ToLower().Contains(searchLower) ||
                x.User!.Email.ToLower().Contains(searchLower) ||
                (x.User!.PhoneNumber != null && x.User.PhoneNumber.Contains(searchLower))
            );
        }

        return specification.SatisfiedBy();
    }

    public static EmployeeSpecification Create(
        string? id = null,
        bool? isActive = null,
        List<ContractStatus>? contractStatuses = null,
        string? searchTerm = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            ContractStatuses = contractStatuses,
            SearchTerm = searchTerm
        };
}
