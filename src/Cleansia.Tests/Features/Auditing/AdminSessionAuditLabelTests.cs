using System.Reflection;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Tenancy;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// Owner ruling 2026-09-15 — an administrator's sign-in and sign-out are admin acts under frozen,
/// query-stable admin labels, resolved through the same descriptor the behavior uses so a class rename
/// never silently changes the string. The sign-in carries the admin marker itself and runs anonymously
/// (the session opens inside it), so it is operator-scoped like every other anonymous session act; the
/// sign-out keeps its customer marker — a customer's sign-out is a customer act — and names the admin
/// label the admin arm writes instead.
/// </summary>
public sealed class AdminSessionAuditLabelTests
{
    [Fact]
    public void The_Admin_SignIn_Carries_The_Frozen_Admin_Label_And_Records_An_Anonymous_Actor()
    {
        var descriptor = AuditActionDescriptor.For(typeof(AdminLogin.Command));

        Assert.Equal("admin.session.login", descriptor.Action);
        Assert.Equal("admin.session.login", descriptor.AdminAction);
        Assert.Equal("User", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Admin, descriptor.Audience);
        Assert.True(descriptor.AllowsAnonymousActor);
        Assert.True(descriptor.Audited);
        Assert.False(descriptor.Sensitive);
    }

    [Fact]
    public void The_Admin_SignIn_Is_Operator_Scoped_To_The_Default_Market_So_Its_Refusal_Rows_Have_A_Tenant()
    {
        IOperatorScopedRequest command = new AdminLogin.Command("someone@cleansia.test", "Secret-123!", RememberMe: true);

        Assert.Null(command.CountryId);
    }

    [Fact]
    public void The_SignOut_Keeps_Its_Customer_Label_And_Names_The_Frozen_Admin_One()
    {
        var descriptor = AuditActionDescriptor.For(typeof(Logout.Command));

        Assert.Equal("customer.session.logout", descriptor.Action);
        Assert.Equal("admin.session.logout", descriptor.AdminAction);
        Assert.Equal("User", descriptor.ResourceType);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.False(descriptor.AllowsAnonymousActor);
    }

    /// <summary>
    /// An admin-audience marker's one label is already the admin one; the descriptor copies
    /// <c>AdminAction</c> regardless of audience, so a second label on such a marker would be written on
    /// every row and read by nobody's contract. None may declare one.
    /// </summary>
    [Fact]
    public void No_Admin_Audience_Marker_Declares_A_Second_Admin_Label()
    {
        var doubled = typeof(IAuditContext).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<AuditActionAttribute>(inherit: false) is { Audience: AuditAudience.Admin, AdminAction: not null })
            .ToList();

        Assert.Empty(doubled);
    }

    /// <summary>The admin sign-in is the one anonymous admin act: nothing else in the assembly may record an anonymous administrator.</summary>
    [Fact]
    public void The_Admin_SignIn_Is_The_Only_Admin_Marker_That_Allows_An_Anonymous_Actor()
    {
        var anonymousAdmin = typeof(IAuditContext).Assembly
            .GetTypes()
            .Where(t => t.GetCustomAttribute<AuditActionAttribute>(inherit: false) is { Audience: AuditAudience.Admin, AllowsAnonymousActor: true })
            .ToList();

        Assert.Equal([typeof(AdminLogin)], anonymousAdmin);
    }
}
