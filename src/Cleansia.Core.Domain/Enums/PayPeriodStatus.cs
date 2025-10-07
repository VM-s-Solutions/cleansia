using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum PayPeriodStatus
{
    Open = 0,
    Closed = 1,
    Paid = 2
}
