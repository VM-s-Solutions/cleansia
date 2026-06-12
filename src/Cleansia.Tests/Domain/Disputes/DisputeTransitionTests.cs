using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Tests.Domain.Disputes;

/// <summary>
/// The dispute status machine. The natural lifecycle frozen for the in-process guard:
///   Pending ↔ UnderReview ↔ WaitingForResponse  → { Closed, Escalated }
///   Escalated → Closed
///   Resolved is reachable ONLY through Resolve (never UpdateStatus)
///   Resolved and Closed are terminal (no re-open).
/// </summary>
public class DisputeTransitionTests
{
    private const string ActorId = "admin-1";

    private static Dispute ArrangeIn(DisputeStatus status)
    {
        var dispute = new Dispute(
            orderId: "order-1",
            userId: "customer-1",
            reason: DisputeReason.Other,
            description: "x",
            createdBy: "customer-1");

        // Drive the entity into the requested starting state through the guarded path itself
        // (Pending is the constructor default; everything else is reached via legal edges).
        switch (status)
        {
            case DisputeStatus.Pending:
                break;
            case DisputeStatus.UnderReview:
                dispute.UpdateStatus(DisputeStatus.UnderReview, ActorId);
                break;
            case DisputeStatus.WaitingForResponse:
                dispute.UpdateStatus(DisputeStatus.WaitingForResponse, ActorId);
                break;
            case DisputeStatus.Escalated:
                dispute.UpdateStatus(DisputeStatus.Escalated, ActorId);
                break;
            case DisputeStatus.Closed:
                dispute.UpdateStatus(DisputeStatus.Closed, ActorId);
                break;
            case DisputeStatus.Resolved:
                dispute.Resolve(ActorId, null, "resolved");
                break;
        }

        return dispute;
    }

    [Theory]
    [InlineData(DisputeStatus.Pending, DisputeStatus.UnderReview)]
    [InlineData(DisputeStatus.Pending, DisputeStatus.WaitingForResponse)]
    [InlineData(DisputeStatus.Pending, DisputeStatus.Closed)]
    [InlineData(DisputeStatus.Pending, DisputeStatus.Escalated)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.Pending)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.WaitingForResponse)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.Closed)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.Escalated)]
    [InlineData(DisputeStatus.WaitingForResponse, DisputeStatus.Pending)]
    [InlineData(DisputeStatus.WaitingForResponse, DisputeStatus.UnderReview)]
    [InlineData(DisputeStatus.WaitingForResponse, DisputeStatus.Closed)]
    [InlineData(DisputeStatus.WaitingForResponse, DisputeStatus.Escalated)]
    [InlineData(DisputeStatus.Escalated, DisputeStatus.Closed)]
    public void Legal_edge_transitions_and_stamps_actor(DisputeStatus from, DisputeStatus to)
    {
        var dispute = ArrangeIn(from);

        var ok = dispute.UpdateStatus(to, ActorId);

        Assert.True(ok);
        Assert.Equal(to, dispute.Status);
        Assert.Equal(ActorId, dispute.UpdatedBy);
    }

    [Theory]
    // Resolved is owned by Resolve — never reachable via UpdateStatus.
    [InlineData(DisputeStatus.Pending, DisputeStatus.Resolved)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.Resolved)]
    [InlineData(DisputeStatus.WaitingForResponse, DisputeStatus.Resolved)]
    [InlineData(DisputeStatus.Escalated, DisputeStatus.Resolved)]
    // Terminal states have no outgoing edges (no re-open).
    [InlineData(DisputeStatus.Resolved, DisputeStatus.Pending)]
    [InlineData(DisputeStatus.Resolved, DisputeStatus.UnderReview)]
    [InlineData(DisputeStatus.Resolved, DisputeStatus.Closed)]
    [InlineData(DisputeStatus.Resolved, DisputeStatus.Escalated)]
    [InlineData(DisputeStatus.Closed, DisputeStatus.Pending)]
    [InlineData(DisputeStatus.Closed, DisputeStatus.UnderReview)]
    [InlineData(DisputeStatus.Closed, DisputeStatus.Resolved)]
    [InlineData(DisputeStatus.Closed, DisputeStatus.Escalated)]
    // Escalated cannot walk back to a working state (no re-open).
    [InlineData(DisputeStatus.Escalated, DisputeStatus.Pending)]
    [InlineData(DisputeStatus.Escalated, DisputeStatus.UnderReview)]
    [InlineData(DisputeStatus.Escalated, DisputeStatus.WaitingForResponse)]
    public void Illegal_edge_is_rejected_and_state_unchanged(DisputeStatus from, DisputeStatus to)
    {
        var dispute = ArrangeIn(from);

        var ok = dispute.UpdateStatus(to, ActorId);

        Assert.False(ok);
        Assert.Equal(from, dispute.Status);
    }

    [Fact]
    public void Same_state_self_edge_is_rejected()
    {
        var dispute = ArrangeIn(DisputeStatus.Pending);

        var ok = dispute.UpdateStatus(DisputeStatus.Pending, ActorId);

        Assert.False(ok);
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
    }

    [Fact]
    public void UpdateStatus_never_writes_resolution_fields()
    {
        var dispute = ArrangeIn(DisputeStatus.Pending);

        dispute.UpdateStatus(DisputeStatus.Closed, ActorId);

        Assert.Null(dispute.RefundAmount);
        Assert.Null(dispute.ResolvedBy);
        Assert.Null(dispute.ResolvedOn);
        Assert.Null(dispute.ResolutionNotes);
    }
}
