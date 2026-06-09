using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum RefundStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3
}
