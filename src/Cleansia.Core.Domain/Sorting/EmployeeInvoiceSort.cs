using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class EmployeeInvoiceSort(string propertyName, bool isAscending)
    : BaseSort<EmployeeInvoice>(propertyName, isAscending)
{
    public override Expression<Func<EmployeeInvoice, object>> DefaultSort => x => x.GeneratedAt;

    protected override Expression<Func<EmployeeInvoice, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(EmployeeInvoice.InvoiceNumber), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.InvoiceNumber;
        }
        if (string.Equals(propertyName, nameof(EmployeeInvoice.TotalAmount), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.TotalAmount;
        }
        if (string.Equals(propertyName, nameof(EmployeeInvoice.Status), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Status;
        }
        if (string.Equals(propertyName, nameof(EmployeeInvoice.GeneratedAt), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.GeneratedAt;
        }
        if (string.Equals(propertyName, nameof(EmployeeInvoice.ApprovedAt), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.ApprovedAt ?? DateTimeOffset.MaxValue;
        }
        if (string.Equals(propertyName, nameof(EmployeeInvoice.PaidAt), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.PaidAt ?? DateTime.MaxValue;
        }
        return DefaultSort;
    }
}
