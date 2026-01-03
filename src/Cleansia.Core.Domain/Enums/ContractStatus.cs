using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum ContractStatus
{
    Pending = 1,
    Active = 2,
    Terminated = 3,
    Approved = 4,
    Rejected = 5
}