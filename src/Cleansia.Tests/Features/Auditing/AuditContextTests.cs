using Cleansia.Core.AppServices.Auditing;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D4 — the scoped snapshot buffer drains exactly what a sensitive handler emitted, serialized
/// to the camelCase JSON the jsonb columns hold, and clears on drain. Pure logic, red-first.
/// </summary>
public sealed class AuditContextTests
{
    private sealed record RefundBefore(string OrderId, decimal Total);

    private sealed record RefundAfter(string OrderId, decimal Refunded);

    private enum Tier { Standard = 0, Plus = 1 }

    private sealed record TierChange(string OrderId, Tier Tier);

    [Fact]
    public void RecordChange_Then_Drain_Returns_The_Typed_Snapshot_Serialized_To_Camelcase()
    {
        var context = new AuditContext();
        context.RecordChange("Order", "ORD-1", new RefundBefore("ORD-1", 100m), new RefundAfter("ORD-1", 25m), "duplicate charge");

        var snapshot = context.DrainSnapshot();

        Assert.NotNull(snapshot);
        Assert.Equal("Order", snapshot!.ResourceType);
        Assert.Equal("ORD-1", snapshot.ResourceId);
        Assert.Equal("duplicate charge", snapshot.Reason);
        Assert.Contains("\"orderId\":\"ORD-1\"", snapshot.BeforeJson);
        Assert.Contains("\"total\":100", snapshot.BeforeJson);
        Assert.Contains("\"refunded\":25", snapshot.AfterJson);
    }

    // A row is read by a support agent three years later and by a lawyer's file; "plus" survives an
    // enum renumbering and needs no lookup, "1" does neither (ADR-0062 D3: enums by name).
    [Fact]
    public void Enums_Are_Serialized_By_Camelcase_Name_On_Both_Record_Paths()
    {
        var context = new AuditContext();

        context.RecordChange("Order", "ORD-1", new TierChange("ORD-1", Tier.Standard), new TierChange("ORD-1", Tier.Plus));
        var change = context.DrainSnapshot();

        context.RecordEvidence("Order", "ORD-1", new TierChange("ORD-1", Tier.Plus));
        var evidence = context.DrainSnapshot();

        Assert.Contains("\"tier\":\"standard\"", change!.BeforeJson);
        Assert.Contains("\"tier\":\"plus\"", change.AfterJson);
        Assert.Contains("\"tier\":\"plus\"", evidence!.AfterJson);
    }

    [Fact]
    public void Drain_With_No_Recorded_Change_Returns_Null()
    {
        var context = new AuditContext();

        Assert.Null(context.DrainSnapshot());
    }

    [Fact]
    public void Drain_Clears_The_Buffer_So_A_Second_Drain_Returns_Null()
    {
        var context = new AuditContext();
        context.RecordChange("Order", "ORD-1", new RefundBefore("ORD-1", 100m), new RefundAfter("ORD-1", 25m));

        Assert.NotNull(context.DrainSnapshot());
        Assert.Null(context.DrainSnapshot());
    }
}
