using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum DocumentStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
