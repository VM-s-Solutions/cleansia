using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum OrderStatus
{
    Pending = 1,
    Confirmed = 2,
    Completed = 3,
    Cancelled = 4
}