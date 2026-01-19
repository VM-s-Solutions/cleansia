using System.Linq.Expressions;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class CompanyInfoSort(string propertyName, bool isAscending)
    : BaseSort<CompanyInfo>(propertyName, isAscending)
{
    public override Expression<Func<CompanyInfo, object>> DefaultSort => x => x.LegalName;

    protected override Expression<Func<CompanyInfo, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(CompanyInfo.LegalName), StringComparison.CurrentCultureIgnoreCase))
            return x => x.LegalName;
        if (string.Equals(propertyName, nameof(CompanyInfo.TradingName), StringComparison.CurrentCultureIgnoreCase))
            return x => x.TradingName;
        if (string.Equals(propertyName, nameof(CompanyInfo.City), StringComparison.CurrentCultureIgnoreCase))
            return x => x.City;
        if (string.Equals(propertyName, nameof(CompanyInfo.CountryId), StringComparison.CurrentCultureIgnoreCase))
            return x => x.CountryId;
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}