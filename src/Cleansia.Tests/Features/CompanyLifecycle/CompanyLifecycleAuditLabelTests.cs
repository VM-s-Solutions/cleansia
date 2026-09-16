using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.CompanyLifecycle;

namespace Cleansia.Tests.Features.CompanyLifecycle;

/// <summary>
/// The lifecycle writers carry frozen, query-stable admin audit labels on the company row, resolved
/// through the same descriptor the behaviour uses, so a class rename never silently changes the string
/// the trail is searched by.
/// </summary>
public sealed class CompanyLifecycleAuditLabelTests
{
    [Theory]
    [InlineData(typeof(DeactivateCompany.Command), "company.deactivate")]
    [InlineData(typeof(ReactivateCompany.Command), "company.reactivate")]
    public void The_Lifecycle_Writers_Carry_The_Frozen_Tenant_Label(Type commandType, string expectedLabel)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expectedLabel, descriptor.Action);
        Assert.Equal("Tenant", descriptor.ResourceType);
        Assert.True(descriptor.Audited);
        Assert.Equal(AuditAudience.Admin, descriptor.Audience);
    }
}
