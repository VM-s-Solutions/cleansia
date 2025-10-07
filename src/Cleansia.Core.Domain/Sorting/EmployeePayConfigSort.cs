using System.Linq.Expressions;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class EmployeePayConfigSort(string propertyName, bool isAscending) : BaseSort<EmployeePayConfig>(propertyName, isAscending)
{
    public override Expression<Func<EmployeePayConfig, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<EmployeePayConfig, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(EmployeePayConfig.BasePay), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.BasePay;
        }
        if (string.Equals(propertyName, nameof(EmployeePayConfig.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }
        return DefaultSort;
    }
}
