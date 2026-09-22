using Cleansia.Core.AppServices.Auditing;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D5 / TC-AUDIT-LABEL — the action label is the normalized command type name by default and
/// a frozen <c>[AuditAction]</c> override where present, rename-proof. The admin arm's label is the
/// marker's <c>AdminAction</c> where a customer marker declares one and the label itself everywhere
/// else, so an administrator's sign-out reads as an admin act (owner ruling 2026-09-15) while a marker
/// that says nothing keeps one label on both arms. Pure logic, red-first.
/// </summary>
public sealed class AuditActionDescriptorTests
{
    public sealed record AdminRefundOrderCommand : ICommandShape;

    [AuditAction("order.refund", Sensitive = true, ResourceType = "Order")]
    public sealed record MarkedRefundCommand : ICommandShape;

    [AuditAction(Audited = false)]
    public sealed record OptedOutCommand : ICommandShape;

    [AuditAction("customer.order.create", Audience = AuditAudience.Customer, ResourceType = "Order", AllowsAnonymousActor = true)]
    public sealed record CustomerMarkedCommand : ICommandShape;

    [AuditAction("customer.session.logout", Audience = AuditAudience.Customer, ResourceType = "User", AdminAction = "admin.session.logout")]
    public sealed record CustomerMarkedWithAdminLabelCommand : ICommandShape;

    [AuditAction("admin.session.login", ResourceType = "User", AllowsAnonymousActor = true)]
    public sealed record AdminMarkedGuestAllowedCommand : ICommandShape;

    public sealed class NestingHost
    {
        [AuditAction("admin.user.create")]
        public sealed record Command : ICommandShape;
    }

    public sealed class UnmarkedNestingHost
    {
        public sealed record Command : ICommandShape;
    }

    public interface ICommandShape;

    [Fact]
    public void Unmarked_Command_Uses_Normalized_TypeName_StrippingCommandSuffix()
    {
        var descriptor = AuditActionDescriptor.For(typeof(AdminRefundOrderCommand));

        Assert.Equal("AdminRefundOrder", descriptor.Action);
        Assert.False(descriptor.Sensitive);
        Assert.True(descriptor.Audited);
        Assert.Null(descriptor.ResourceType);
    }

    [Fact]
    public void Unmarked_And_Admin_Marked_Commands_Are_The_Admin_Audience_And_Refuse_Anonymous_Actors()
    {
        Assert.Equal(AuditAudience.Admin, AuditActionDescriptor.For(typeof(AdminRefundOrderCommand)).Audience);
        Assert.Equal(AuditAudience.Admin, AuditActionDescriptor.For(typeof(MarkedRefundCommand)).Audience);
        Assert.False(AuditActionDescriptor.For(typeof(AdminRefundOrderCommand)).AllowsAnonymousActor);
        Assert.False(AuditActionDescriptor.For(typeof(MarkedRefundCommand)).AllowsAnonymousActor);
    }

    [Fact]
    public void A_Customer_Marker_Copies_Audience_And_AllowsAnonymousActor_From_The_Marker()
    {
        var descriptor = AuditActionDescriptor.For(typeof(CustomerMarkedCommand));

        Assert.Equal("customer.order.create", descriptor.Action);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
        Assert.True(descriptor.AllowsAnonymousActor);
        Assert.Equal("Order", descriptor.ResourceType);
    }

    [Fact]
    public void A_Customer_Marker_With_An_Admin_Label_Keeps_Its_Own_Label_And_Resolves_The_Admin_One()
    {
        var descriptor = AuditActionDescriptor.For(typeof(CustomerMarkedWithAdminLabelCommand));

        Assert.Equal("customer.session.logout", descriptor.Action);
        Assert.Equal("admin.session.logout", descriptor.AdminAction);
        Assert.Equal(AuditAudience.Customer, descriptor.Audience);
    }

    [Theory]
    [InlineData(typeof(AdminRefundOrderCommand), "AdminRefundOrder")]
    [InlineData(typeof(MarkedRefundCommand), "order.refund")]
    [InlineData(typeof(CustomerMarkedCommand), "customer.order.create")]
    [InlineData(typeof(NestingHost.Command), "admin.user.create")]
    public void A_Marker_Without_An_Admin_Label_Resolves_The_Same_Label_On_Both_Arms(Type commandType, string expected)
    {
        var descriptor = AuditActionDescriptor.For(commandType);

        Assert.Equal(expected, descriptor.Action);
        Assert.Equal(expected, descriptor.AdminAction);
    }

    [Fact]
    public void An_Admin_Marker_May_Allow_An_Anonymous_Actor()
    {
        var descriptor = AuditActionDescriptor.For(typeof(AdminMarkedGuestAllowedCommand));

        Assert.Equal("admin.session.login", descriptor.Action);
        Assert.Equal("admin.session.login", descriptor.AdminAction);
        Assert.Equal(AuditAudience.Admin, descriptor.Audience);
        Assert.True(descriptor.AllowsAnonymousActor);
        Assert.Equal("User", descriptor.ResourceType);
    }

    [Fact]
    public void Unmarked_Nested_Command_Record_Uses_DeclaringType_Name()
    {
        var descriptor = AuditActionDescriptor.For(typeof(UnmarkedNestingHost.Command));

        Assert.Equal("UnmarkedNestingHost", descriptor.Action);
    }

    [Fact]
    public void Marked_Command_Freezes_The_Label_And_Flags_Sensitive_And_ResourceType()
    {
        var descriptor = AuditActionDescriptor.For(typeof(MarkedRefundCommand));

        Assert.Equal("order.refund", descriptor.Action);
        Assert.True(descriptor.Sensitive);
        Assert.Equal("Order", descriptor.ResourceType);
        Assert.True(descriptor.Audited);
    }

    [Fact]
    public void Marker_On_The_DeclaringType_Is_Read_For_A_Nested_Command_Record()
    {
        var descriptor = AuditActionDescriptor.For(typeof(NestingHost.Command));

        Assert.Equal("admin.user.create", descriptor.Action);
    }

    [Fact]
    public void Audited_False_Marker_Opts_The_Command_Out()
    {
        var descriptor = AuditActionDescriptor.For(typeof(OptedOutCommand));

        Assert.False(descriptor.Audited);
    }

    [Fact]
    public void A_Frozen_Label_Does_Not_Change_When_The_Class_Is_Renamed()
    {
        // The frozen label is the marker string, independent of the type name — renaming the type (here
        // simulated by two differently-named types carrying the SAME marker) yields the same label.
        var first = AuditActionDescriptor.For(typeof(MarkedRefundCommand));
        var second = AuditActionDescriptor.For(typeof(RenamedButSameMarkerCommand));

        Assert.Equal(first.Action, second.Action);
        Assert.Equal("order.refund", second.Action);
    }

    [AuditAction("order.refund", Sensitive = true, ResourceType = "Order")]
    public sealed record RenamedButSameMarkerCommand : ICommandShape;
}
