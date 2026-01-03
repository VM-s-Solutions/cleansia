using System.Linq.Expressions;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class ServiceSort(string propertyName, bool isAscending)
    : BaseSort<Service>(propertyName, isAscending)
{
    public override Expression<Func<Service, object>> DefaultSort => x => x.Name;

    protected override Expression<Func<Service, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Service.Name), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Name;
        if (string.Equals(propertyName, nameof(Service.BasePrice), StringComparison.CurrentCultureIgnoreCase))
            return x => x.BasePrice;
        if (string.Equals(propertyName, nameof(Service.PerRoomPrice), StringComparison.CurrentCultureIgnoreCase))
            return x => x.PerRoomPrice;
        if (string.Equals(propertyName, nameof(Service.EstimatedTime), StringComparison.CurrentCultureIgnoreCase))
            return x => x.EstimatedTime;
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}