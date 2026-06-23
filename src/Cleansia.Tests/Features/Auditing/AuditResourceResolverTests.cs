using Cleansia.Core.AppServices.Auditing;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D5.1 — best-effort conventional resource-id read off the command. Pure logic, red-first.
/// Unresolvable returns null (the row is still written with a null ResourceId).
/// </summary>
public sealed class AuditResourceResolverTests
{
    public sealed record OrderCommand(string OrderId);

    public sealed record IdCommand(string Id);

    public sealed record SingleIdLikeCommand(string DisputeId, string Message);

    public sealed record AmbiguousCommand(string OrderId, string EmployeeId);

    public sealed record NoIdCommand(string Note);

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
}
