using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Enums;

/// <summary>
/// What a refund means to the people who owe it, read from its status, failed date and repaid date.
/// → /flows/cancellation-refund-dispute#refund
/// </summary>
[SwaggerEnumAsInt]
public enum RefundState
{
    NotSent = 1,
    Refunded = 2,
    Failed = 3,
    Bounced = 4,
    Repaid = 5,
    Closed = 6,
}
