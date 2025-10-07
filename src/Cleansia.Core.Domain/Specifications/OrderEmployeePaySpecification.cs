using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class OrderEmployeePaySpecification : BaseSpecification<string?>, ISpecification<OrderEmployeePay>
{
    public string? EmployeeId { get; set; }
    public string? OrderId { get; set; }
    public DateOnly? Date { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? EmployeeInvoiceId { get; set; }
    public bool? IsApproved { get; set; }
    public bool? IsUnassigned { get; set; }

    public Expression<Func<OrderEmployeePay, bool>> SatisfiedBy()
    {
        Specification<OrderEmployeePay> specification = new TrueSpecification<OrderEmployeePay>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(EmployeeId))
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => x.EmployeeId == EmployeeId);
        }

        if (!string.IsNullOrWhiteSpace(OrderId))
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => x.OrderId == OrderId);
        }

        if (Date.HasValue)
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => 
                DateOnly.FromDateTime(x.CreatedOn.Date) == Date.Value);
        }

        if (StartDate.HasValue)
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => 
                DateOnly.FromDateTime(x.CreatedOn.Date) >= StartDate.Value);
        }

        if (EndDate.HasValue)
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => 
                DateOnly.FromDateTime(x.CreatedOn.Date) <= EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(EmployeeInvoiceId))
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => x.EmployeeInvoiceId == EmployeeInvoiceId);
        }

        if (IsApproved.HasValue)
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => x.IsApproved == IsApproved.Value);
        }

        if (IsUnassigned.HasValue && IsUnassigned.Value)
        {
            specification &= new DirectSpecification<OrderEmployeePay>(x => x.EmployeeInvoiceId == null);
        }

        return specification.SatisfiedBy();
    }

    public static OrderEmployeePaySpecification Create(
        string? id = null,
        bool? isActive = null,
        string? employeeId = null,
        string? orderId = null,
        DateOnly? date = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? employeeInvoiceId = null,
        bool? isApproved = null,
        bool? isUnassigned = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            EmployeeId = employeeId,
            OrderId = orderId,
            Date = date,
            StartDate = startDate,
            EndDate = endDate,
            EmployeeInvoiceId = employeeInvoiceId,
            IsApproved = isApproved,
            IsUnassigned = isUnassigned
        };
}
