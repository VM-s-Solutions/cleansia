using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Employees.Filters;

public class EmployeeFilter
{
    public string? Id { get; init; }
    public bool? IsActive { get; init; }
    public List<ContractStatus>? ContractStatuses { get; init; }
    public string? SearchTerm { get; init; }
}
