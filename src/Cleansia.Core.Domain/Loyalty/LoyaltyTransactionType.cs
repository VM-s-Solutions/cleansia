using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Loyalty;

[SwaggerEnumAsInt]
public enum LoyaltyTransactionType
{
    Earn = 1,
    Revoke = 2,
}
