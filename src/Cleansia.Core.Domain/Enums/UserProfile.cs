using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum UserProfile
{
    Customer = 1,
    Employee = 2,
    Administrator = 100,
}
