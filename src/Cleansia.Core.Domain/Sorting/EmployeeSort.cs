using System.Linq.Expressions;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Sorting;

public class EmployeeSort(string propertyName, bool isAscending)
    : BaseSort<Employee>(propertyName, isAscending)
{
    public override Expression<Func<Employee, object>> DefaultSort => x => x.User!.CreatedOn;

    protected override Expression<Func<Employee, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, "FirstName", StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "Name", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.User!.FirstName;
        }
        if (string.Equals(propertyName, "LastName", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.User!.LastName;
        }
        if (string.Equals(propertyName, "Email", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.User!.Email;
        }
        if (string.Equals(propertyName, nameof(Employee.ContractStatus), StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "Status", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.ContractStatus;
        }
        if (string.Equals(propertyName, nameof(Employee.AverageRating), StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "Rating", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.AverageRating;
        }
        if (string.Equals(propertyName, "CreatedAt", StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.User!.CreatedOn;
        }
        return DefaultSort;
    }
}
