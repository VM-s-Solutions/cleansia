using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class PayPeriodSort(string propertyName, bool isAscending) : BaseSort<PayPeriod>(propertyName, isAscending)
{
    public override Expression<Func<PayPeriod, object>> DefaultSort => x => x.Status;

    protected override Expression<Func<PayPeriod, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(PayPeriod.StartDate), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.StartDate;
        }
        if (string.Equals(propertyName, nameof(PayPeriod.EndDate), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.EndDate;
        }
        if (string.Equals(propertyName, nameof(PayPeriod.Status), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Status;
        }
        if (string.Equals(propertyName, nameof(PayPeriod.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }
        return DefaultSort;
    }
}
