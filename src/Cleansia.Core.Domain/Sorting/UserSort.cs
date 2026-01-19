using System.Linq.Expressions;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;

namespace Cleansia.Core.Domain.Sorting;

public class UserSort(string propertyName, bool isAscending)
    : BaseSort<User>(propertyName, isAscending)
{
    public override Expression<Func<User, object>> DefaultSort => x => x.LastName;

    protected override Expression<Func<User, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(User.FirstName), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.FirstName;
        }
        if (string.Equals(propertyName, nameof(User.LastName), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.LastName;
        }
        if (string.Equals(propertyName, nameof(User.Email), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Email;
        }
        if (string.Equals(propertyName, nameof(User.PhoneNumber), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.PhoneNumber;
        }
        if (string.Equals(propertyName, nameof(User.BirthDate), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.BirthDate;
        }
        if (string.Equals(propertyName, nameof(User.Profile), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Profile;
        }
        if (string.Equals(propertyName, nameof(User.AuthenticationType), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.AuthenticationType;
        }
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }
        return DefaultSort;
    }
}