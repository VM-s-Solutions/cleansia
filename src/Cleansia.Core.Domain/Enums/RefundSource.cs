using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum RefundSource
{
    AppRefund = 1,
    Chargeback = 2
}
