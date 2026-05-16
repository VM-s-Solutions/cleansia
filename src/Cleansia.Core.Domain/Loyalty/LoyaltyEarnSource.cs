using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Loyalty;

[SwaggerEnumAsInt]
public enum LoyaltyEarnSource
{
    OrderCompleted = 1,
    OrderCancelled = 2,
    Referral = 3,
    ManualGrant = 4,
}
