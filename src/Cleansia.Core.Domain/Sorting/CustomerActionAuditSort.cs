using System.Linq.Expressions;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class CustomerActionAuditSort(string propertyName, bool isAscending)
    : BaseSort<CustomerActionAudit>(propertyName, isAscending)
{
    public override Expression<Func<CustomerActionAudit, object>> DefaultSort => x => x.OccurredOn;

    protected override Expression<Func<CustomerActionAudit, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(CustomerActionAudit.OccurredOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.OccurredOn;
        }
        if (string.Equals(propertyName, nameof(CustomerActionAudit.UserId), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.UserId ?? string.Empty;
        }
        if (string.Equals(propertyName, nameof(CustomerActionAudit.Action), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Action;
        }
        if (string.Equals(propertyName, nameof(CustomerActionAudit.ResourceType), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.ResourceType ?? string.Empty;
        }
        if (string.Equals(propertyName, nameof(CustomerActionAudit.Success), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Success;
        }
        return DefaultSort;
    }
}
