using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum RefundOrigin
{
    Cancellation = 1,
    Dispute = 2,
    FullRefund = 3,
    PartialRefund = 4,
}
