using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum EmployeeInvoiceStatus
{
    Pending = 1,
    Approved = 2,
    Paid = 3,
    Disputed = 4,
    Rejected = 5,
    Cancelled = 6
}
