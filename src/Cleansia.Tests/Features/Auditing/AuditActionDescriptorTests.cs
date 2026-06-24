using Cleansia.Core.AppServices.Auditing;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D5 / TC-AUDIT-LABEL — the action label is the normalized command type name by default and
/// a frozen <c>[AuditAction]</c> override where present, rename-proof. Pure logic, red-first.
/// </summary>
public sealed class AuditActionDescriptorTests
{
    public sealed record AdminRefundOrderCommand : ICommandShape;

    [AuditAction("order.refund", Sensitive = true, ResourceType = "Order")]
    public sealed record MarkedRefundCommand : ICommandShape;

    [AuditAction(Audited = false)]
    public sealed record OptedOutCommand : ICommandShape;

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
