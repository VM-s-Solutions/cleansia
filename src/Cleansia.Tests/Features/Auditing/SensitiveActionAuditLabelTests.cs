using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Features.Refunds;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D4/D5 / AC5 — each sensitive (five + dispute) command carries a frozen, query-stable
/// <c>[AuditAction]</c> label with <c>Sensitive = true</c> and a <c>ResourceType</c>, resolved through the
/// same descriptor the behavior uses. A class rename can no longer silently change the audit string, and
/// the sensitive subset is mechanically identifiable.
/// </summary>
public sealed class SensitiveActionAuditLabelTests
{
    [Theory]
    [InlineData(typeof(IssuePartialRefund.Command), "order.refund.partial", "Order")]
    [InlineData(typeof(AdminRefundOrder.Command), "order.refund.full", "Order")]
    [InlineData(typeof(AdminOverrideOrderStatus.Command), "order.status.override", "Order")]
    [InlineData(typeof(ResolveDispute.Command), "dispute.resolve", "Dispute")]
    [InlineData(typeof(UpdatePayConfig.Command), "payconfig.update", "EmployeePayConfig")]
    [InlineData(typeof(GrantPointsManually.Command), "loyalty.points.grant", "LoyaltyAccount")]
    [InlineData(typeof(RevokePointsManually.Command), "loyalty.points.revoke", "LoyaltyAccount")]
    [InlineData(typeof(AdminDeleteUserAccount.Command), "gdpr.user.delete", "User")]
    public void Sensitive_Commands_Carry_The_Frozen_Sensitive_Label(Type commandType, string expectedLabel, string expectedResourceType)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal(expectedResourceType, descriptor.ResourceType);
        Assert.True(descriptor.Sensitive);
        Assert.True(descriptor.Audited);
    }
}
