using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3
}