using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class EmployeeInvoiceSpecification : BaseSpecification<string?>, ISpecification<EmployeeInvoice>
{
    public string? EmployeeId { get; set; }
    public string? PayPeriodId { get; set; }
    public EmployeeInvoiceStatus? Status { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? VariableSymbol { get; set; }
    public int[]? Statuses { get; set; }

    public Expression<Func<EmployeeInvoice, bool>> SatisfiedBy()
    {
        Specification<EmployeeInvoice> specification = new TrueSpecification<EmployeeInvoice>();

        if (!string.IsNullOrWhiteSpace(Id))
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.Id == Id);
        }

        if (IsActive.HasValue)
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.IsActive == IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(EmployeeId))
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.EmployeeId == EmployeeId);
        }

        if (!string.IsNullOrWhiteSpace(PayPeriodId))
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.PayPeriodId == PayPeriodId);
        }

        if (Status.HasValue)
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.Status == Status.Value);
        }

        if (Statuses is not null && Statuses.Any())
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => Statuses.Contains((int)x.Status));
        }

        if (!string.IsNullOrWhiteSpace(InvoiceNumber))
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.InvoiceNumber == InvoiceNumber);
        }

        if (!string.IsNullOrWhiteSpace(VariableSymbol))
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.VariableSymbol == VariableSymbol);
        }

        return specification.SatisfiedBy();
    }

    public static EmployeeInvoiceSpecification Create(
        string? id = null,
        bool? isActive = null,
        string? employeeId = null,
        string? payPeriodId = null,
        EmployeeInvoiceStatus? status = null,
        int[]? statuses = null,
        string? invoiceNumber = null,
        string? variableSymbol = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            EmployeeId = employeeId,
            PayPeriodId = payPeriodId,
            Status = status,
            Statuses = statuses,
            InvoiceNumber = invoiceNumber,
            VariableSymbol = variableSymbol
        };
}
