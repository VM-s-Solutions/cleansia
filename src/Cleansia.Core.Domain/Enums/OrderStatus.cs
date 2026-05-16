using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum OrderStatus
{
    New = 0,
    Pending = 1,
    Confirmed = 2,
    OnTheWay = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6,
}