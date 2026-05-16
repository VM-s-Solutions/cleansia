using Cleansia.Infra.Common.Attributes;

namespace Cleansia.Core.Domain.Loyalty;

[SwaggerEnumAsInt]
public enum ReferralStatus
{
    /// <summary>The invitee has redeemed the code but has not yet completed a qualifying order.</summary>
    Accepted = 1,

    /// <summary>The invitee's first completed order has triggered the +150 grant on both sides.</summary>
    Qualified = 2,

    /// <summary>The 90-day qualifying window passed without a completed order. No points are granted.</summary>
    Expired = 3,
}
