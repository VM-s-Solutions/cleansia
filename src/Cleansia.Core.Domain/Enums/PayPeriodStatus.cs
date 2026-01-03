using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum PayPeriodStatus
{
    Open = 1,
    Closed = 2,
    Paid = 3
}
