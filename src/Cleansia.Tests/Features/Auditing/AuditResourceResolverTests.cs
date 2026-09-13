using Cleansia.Core.AppServices.Auditing;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D5.1 — best-effort conventional resource-id read off the command. Pure logic, red-first.
/// Unresolvable returns null (the row is still written with a null ResourceId).
///
/// <para>ADR-0062 D1 — the customer arm reads <see cref="AuditResourceResolver.ResolveExact"/>: the
/// <c>{ResourceType}Id</c> property only, neither fallback. The fallbacks would label a checkout's
/// <c>CountryId</c> a membership and a dispute filing's <c>OrderId</c> a dispute.</para>
public sealed class AuditResourceResolverTests
{
    public sealed record OrderCommand(string OrderId);

    public sealed record IdCommand(string Id);

    public sealed record SingleIdLikeCommand(string DisputeId, string Message);

    public sealed record AmbiguousCommand(string OrderId, string EmployeeId);

    public sealed record NoIdCommand(string Note);

    public sealed record CheckoutSessionCommand(string PlanCode, string? CountryId);

    public sealed record ScheduleCommand(string TemplateId);

    [Fact]
    public void Reads_The_ResourceType_Prefixed_Id_When_The_Marker_Named_A_Resource()
    {
        var resolved = AuditResourceResolver.ResolveResourceId(new OrderCommand("ORD-1"), "Order");

        Assert.Equal("ORD-1", resolved);
    }

    [Fact]
    public void Falls_Back_To_A_Conventional_Id_Property()
    {
        var resolved = AuditResourceResolver.ResolveResourceId(new IdCommand("ULID-1"), resourceType: null);

        Assert.Equal("ULID-1", resolved);
    }

    [Fact]
    public void Reads_The_Single_IdLike_Property_When_There_Is_Exactly_One()
    {
        var resolved = AuditResourceResolver.ResolveResourceId(new SingleIdLikeCommand("DSP-1", "hi"), resourceType: null);

        Assert.Equal("DSP-1", resolved);
    }

    [Fact]
    public void Returns_Null_When_Multiple_IdLike_Properties_Are_Ambiguous()
    {
        var resolved = AuditResourceResolver.ResolveResourceId(new AmbiguousCommand("ORD-1", "EMP-1"), resourceType: null);

        Assert.Null(resolved);
    }

    [Fact]
    public void Returns_Null_When_No_Id_Is_Resolvable()
    {
        var resolved = AuditResourceResolver.ResolveResourceId(new NoIdCommand("just a note"), resourceType: null);

        Assert.Null(resolved);
    }

    // ── ResolveExact, the customer arm ────────────────────────────────────────

    [Fact]
    public void Exact_Reads_The_ResourceType_Prefixed_Id()
    {
        Assert.Equal("ORD-1", AuditResourceResolver.ResolveExact(new OrderCommand("ORD-1"), "Order"));
    }

    [Fact]
    public void Exact_Never_Falls_Back_To_A_Conventional_Id_Property()
    {
        Assert.Null(AuditResourceResolver.ResolveExact(new IdCommand("ULID-1"), "Order"));
    }

    [Fact]
    public void Exact_Never_Falls_Back_To_The_Single_IdLike_Property()
    {
        Assert.Null(AuditResourceResolver.ResolveExact(new SingleIdLikeCommand("DSP-1", "hi"), "Order"));
    }

    [Fact]
    public void Exact_Never_Labels_A_Checkouts_CountryId_As_The_Membership()
    {
        Assert.Null(AuditResourceResolver.ResolveExact(new CheckoutSessionCommand("plus", "CZ"), "UserMembership"));
    }

    [Fact]
    public void Exact_Returns_Null_When_The_Marker_Named_No_Resource_Type()
    {
        Assert.Null(AuditResourceResolver.ResolveExact(new OrderCommand("ORD-1"), resourceType: null));
    }

    [Fact]
    public void Exact_Reads_The_Property_The_Marker_Named_When_The_Wire_Name_Is_Not_The_Convention()
    {
        Assert.Null(AuditResourceResolver.ResolveExact(new ScheduleCommand("tpl-1"), "RecurringBookingTemplate"));
        Assert.Equal("tpl-1", AuditResourceResolver.ResolveExact(new ScheduleCommand("tpl-1"), "RecurringBookingTemplate", "TemplateId"));
    }

    [Fact]
    public void A_Named_Property_Still_Needs_A_Resource_Type_And_Is_Still_Exact()
    {
        Assert.Null(AuditResourceResolver.ResolveExact(new ScheduleCommand("tpl-1"), resourceType: null, "TemplateId"));
        Assert.Null(AuditResourceResolver.ResolveExact(new ScheduleCommand("tpl-1"), "RecurringBookingTemplate", "Id"));
    }
}
