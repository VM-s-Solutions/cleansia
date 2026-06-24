using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.AdminUsers;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D3.1 / AC8 — the SuperAdmin / privilege-management admin-user commands carry a frozen,
/// query-stable <c>[AuditAction]</c> label so a class rename never silently changes the audit string for
/// the highest-value audit target. Resolved through the same descriptor the behavior uses.
/// </summary>
public sealed class AdminUserPrivilegeAuditLabelTests
{
    [Theory]
    [InlineData(typeof(CreateAdminUser.Command), "admin.user.create")]
    [InlineData(typeof(DeactivateAdminUser.Command), "admin.user.deactivate")]
    [InlineData(typeof(ActivateAdminUser.Command), "admin.user.activate")]
    [InlineData(typeof(UpdateAdminUser.Command), "admin.user.update")]
    public void Privilege_Commands_Carry_The_Frozen_AdminUser_Label(Type commandType, string expectedLabel)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal("AdminUser", descriptor.ResourceType);
        Assert.True(descriptor.Audited);
    }
}
