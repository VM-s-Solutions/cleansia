using System.Linq.Expressions;
using Cleansia.Core.Domain.ReceiptTemplates;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class ReceiptTemplateSort(string propertyName, bool isAscending)
    : BaseSort<ReceiptTemplate>(propertyName, isAscending)
{
    public override Expression<Func<ReceiptTemplate, object>> DefaultSort => x => x.TemplateName;

    protected override Expression<Func<ReceiptTemplate, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(ReceiptTemplate.TemplateName), StringComparison.CurrentCultureIgnoreCase))
            return x => x.TemplateName;
        if (string.Equals(propertyName, nameof(ReceiptTemplate.CountryId), StringComparison.CurrentCultureIgnoreCase))
            return x => x.CountryId;
        if (string.Equals(propertyName, nameof(ReceiptTemplate.LanguageId), StringComparison.CurrentCultureIgnoreCase))
            return x => x.LanguageId;
        if (string.Equals(propertyName, nameof(ReceiptTemplate.Version), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Version;
        if (string.Equals(propertyName, nameof(ReceiptTemplate.IsActive), StringComparison.CurrentCultureIgnoreCase))
            return x => x.IsActive;
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}