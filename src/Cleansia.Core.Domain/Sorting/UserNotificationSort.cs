using System.Linq.Expressions;
using Cleansia.Core.Domain.Notifications;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class UserNotificationSort(string propertyName, bool isAscending)
    : BaseSort<UserNotification>(propertyName, isAscending)
{
    public override Expression<Func<UserNotification, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<UserNotification, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(UserNotification.CreatedOn), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }

        return DefaultSort;
    }
}
