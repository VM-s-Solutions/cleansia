using System.Linq.Expressions;
using Cleansia.Core.Domain.Emails;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class EmailTemplateTranslationSort(string propertyName, bool isAscending)
    : BaseSort<EmailTemplateTranslation>(propertyName, isAscending)
{
    public override Expression<Func<EmailTemplateTranslation, object>> DefaultSort => x => x.Key;

    protected override Expression<Func<EmailTemplateTranslation, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(EmailTemplateTranslation.Key), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Key;
        if (string.Equals(propertyName, nameof(EmailTemplateTranslation.EmailType), StringComparison.CurrentCultureIgnoreCase))
            return x => x.EmailType;
        if (string.Equals(propertyName, nameof(EmailTemplateTranslation.LanguageId), StringComparison.CurrentCultureIgnoreCase))
            return x => x.LanguageId;
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}