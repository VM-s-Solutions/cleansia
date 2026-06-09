using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

[SwaggerEnumAsInt]
public enum RefundReason
{
    CustomerCancellation = 1,
    DisputeResolution = 2,
    AdminDiscretion = 3,
    ServiceNotRendered = 4
}
