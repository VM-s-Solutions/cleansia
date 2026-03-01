using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum GdprRequestStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}
