using System.Linq.Expressions;
using Cleansia.Core.Domain.InvoiceTemplates;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class InvoiceTemplateSort(string propertyName, bool isAscending)
    : BaseSort<InvoiceTemplate>(propertyName, isAscending)
{
    public override Expression<Func<InvoiceTemplate, object>> DefaultSort => x => x.TemplateName;

    protected override Expression<Func<InvoiceTemplate, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(InvoiceTemplate.TemplateName), StringComparison.CurrentCultureIgnoreCase))
            return x => x.TemplateName;
        if (string.Equals(propertyName, nameof(InvoiceTemplate.CountryId), StringComparison.CurrentCultureIgnoreCase))
            return x => x.CountryId;
        if (string.Equals(propertyName, nameof(InvoiceTemplate.LanguageId), StringComparison.CurrentCultureIgnoreCase))
            return x => x.LanguageId;
        if (string.Equals(propertyName, nameof(InvoiceTemplate.Version), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Version;
        if (string.Equals(propertyName, nameof(InvoiceTemplate.IsActive), StringComparison.CurrentCultureIgnoreCase))
            return x => x.IsActive;
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}