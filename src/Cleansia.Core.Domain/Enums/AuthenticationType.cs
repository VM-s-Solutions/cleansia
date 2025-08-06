using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum AuthenticationType
{
    Internal = 1,
    Google = 2,
}
