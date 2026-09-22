using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.TenantSettings;

namespace Cleansia.Tests.Features.TenantSettings;

/// <summary>
/// The two writers of a company's configuration carry frozen, query-stable audit labels, resolved
/// through the same descriptor the behaviour uses, so a class rename never silently changes the string
/// the trail is searched by.
/// </summary>
public sealed class TenantSettingAuditLabelTests
{
    [Theory]
    [InlineData(typeof(SetTenantSetting.Command), "tenant_setting.set")]
    [InlineData(typeof(ResetTenantSetting.Command), "tenant_setting.reset")]
    public void The_Setting_Writers_Carry_The_Frozen_TenantSetting_Label(Type commandType, string expectedLabel)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal("TenantSetting", descriptor.ResourceType);
        Assert.True(descriptor.Audited);
        Assert.Equal(AuditAudience.Admin, descriptor.Audience);
    }
}
