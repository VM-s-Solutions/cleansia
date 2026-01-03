using System.Linq.Expressions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class EmployeeDocumentSort(string propertyName, bool isAscending)
    : BaseSort<EmployeeDocument>(propertyName, isAscending)
{
    public override Expression<Func<EmployeeDocument, object>> DefaultSort => x => x.CreatedOn;

    protected override Expression<Func<EmployeeDocument, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(EmployeeDocument.FileName), StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "Name", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.FileName;
        }
        if (string.Equals(propertyName, nameof(EmployeeDocument.DocumentType), StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "Type", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.DocumentType;
        }
        if (string.Equals(propertyName, nameof(EmployeeDocument.Status), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Status;
        }
        if (string.Equals(propertyName, nameof(EmployeeDocument.Version), StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.Version;
        }
        if (string.Equals(propertyName, nameof(EmployeeDocument.FileSizeBytes), StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "Size", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.FileSizeBytes;
        }
        if (string.Equals(propertyName, "CreatedAt", StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.CreatedOn;
        }
        if (string.Equals(propertyName, "UpdatedAt", StringComparison.CurrentCultureIgnoreCase) ||
            string.Equals(propertyName, "UpdatedOn", StringComparison.CurrentCultureIgnoreCase))
        {
            return x => x.UpdatedOn ?? x.CreatedOn;
        }
        return DefaultSort;
    }
}
