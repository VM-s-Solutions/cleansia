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
    public EmployeeInvoiceStatus[]? Statuses { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

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
            specification &= new DirectSpecification<EmployeeInvoice>(x => Statuses.Contains(x.Status));
        }

        if (!string.IsNullOrWhiteSpace(InvoiceNumber))
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.InvoiceNumber.Contains(InvoiceNumber));
        }

        if (!string.IsNullOrWhiteSpace(VariableSymbol))
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.VariableSymbol == VariableSymbol);
        }

        if (MinAmount.HasValue)
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.TotalAmount >= MinAmount.Value);
        }

        if (MaxAmount.HasValue)
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.TotalAmount <= MaxAmount.Value);
        }

        if (DateFrom.HasValue)
        {
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.GeneratedAt >= DateFrom.Value);
        }

        if (DateTo.HasValue)
        {
            // Include the entire day by going to end of day
            var dateTo = DateTo.Value.Date.AddDays(1).AddTicks(-1);
            specification &= new DirectSpecification<EmployeeInvoice>(x => x.GeneratedAt <= dateTo);
        }

        return specification.SatisfiedBy();
    }

    public static EmployeeInvoiceSpecification Create(
        string? id = null,
        bool? isActive = null,
        string? employeeId = null,
        string? payPeriodId = null,
        EmployeeInvoiceStatus? status = null,
        EmployeeInvoiceStatus[]? statuses = null,
        string? invoiceNumber = null,
        string? variableSymbol = null,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null) =>
        new()
        {
            Id = id,
            IsActive = isActive,
            EmployeeId = employeeId,
            PayPeriodId = payPeriodId,
            Status = status,
            Statuses = statuses,
            InvoiceNumber = invoiceNumber,
            VariableSymbol = variableSymbol,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            DateFrom = dateFrom,
            DateTo = dateTo
        };
}
