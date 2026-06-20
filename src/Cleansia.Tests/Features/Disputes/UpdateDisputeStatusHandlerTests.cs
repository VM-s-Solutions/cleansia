using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// The transition guard in the UpdateDisputeStatus write path. Legal edges succeed and stamp
/// the actor; illegal edges return Failure(dispute.invalid_status_transition) with the status
/// unchanged and no resolution-field mutation (Resolve owns those). Authorization is unchanged
/// (Policy.CanUpdateDisputeStatus, Admin) — asserted in the host authz tests, not here.
/// </summary>
public class UpdateDisputeStatusHandlerTests
{
    private const string DisputeId = "dispute-1";
    private const string ActorId = "admin-9";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public UpdateDisputeStatusHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(ActorId);
    }

    private UpdateDisputeStatus.Handler CreateHandler() =>
        new(_disputeRepository.Object, _session.Object);

    private Dispute ArrangeDispute(DisputeStatus status)
    {
        var dispute = new Dispute(
            orderId: "order-1",
            userId: "customer-1",
            reason: DisputeReason.Other,
            description: "x",
            createdBy: "customer-1")
        {
            Id = DisputeId,
        };

        // Reach the start state: Resolved is owned by Resolve; everything else via a legal edge.
        if (status == DisputeStatus.Resolved)
        {
            dispute.Resolve(ActorId, null, "resolved");
        }
        else if (status != DisputeStatus.Pending)
        {
            dispute.UpdateStatus(status, ActorId);
        }

        _disputeRepository
            .Setup(r => r.GetForUpdateAsync(DisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
        return dispute;
    }

    [Theory]
    [InlineData(DisputeStatus.Pending, DisputeStatus.UnderReview)]
    [InlineData(DisputeStatus.Pending, DisputeStatus.Closed)]
    [InlineData(DisputeStatus.Pending, DisputeStatus.Escalated)]
    [InlineData(DisputeStatus.UnderReview, DisputeStatus.WaitingForResponse)]
    [InlineData(DisputeStatus.Escalated, DisputeStatus.Closed)]
    public async Task Legal_edge_succeeds_and_sets_status(DisputeStatus from, DisputeStatus to)
    {
        var dispute = ArrangeDispute(from);

        var result = await CreateHandler().Handle(
            new UpdateDisputeStatus.Command(DisputeId, to), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DisputeId, result.Value!.DisputeId);
        Assert.Equal(to, result.Value.Status);
        Assert.Equal(to, dispute.Status);
        Assert.Equal(ActorId, dispute.UpdatedBy);
    }

    [Theory]
    [InlineData(DisputeStatus.Pending, DisputeStatus.Resolved)]
    [InlineData(DisputeStatus.Resolved, DisputeStatus.Pending)]
    [InlineData(DisputeStatus.Closed, DisputeStatus.Escalated)]
    [InlineData(DisputeStatus.Escalated, DisputeStatus.UnderReview)]
    public async Task Illegal_edge_fails_with_invalid_transition_code_and_does_not_mutate(
        DisputeStatus from, DisputeStatus to)
    {
        var dispute = ArrangeDispute(from);

        var result = await CreateHandler().Handle(
            new UpdateDisputeStatus.Command(DisputeId, to), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidDisputeStatusTransition, result.Error!.Message);
        Assert.Equal(from, dispute.Status);
    }

    [Fact]
    public async Task UpdateStatus_cannot_set_Resolved_and_leaves_resolution_fields_unwritten()
    {
        var dispute = ArrangeDispute(DisputeStatus.UnderReview);

        var result = await CreateHandler().Handle(
            new UpdateDisputeStatus.Command(DisputeId, DisputeStatus.Resolved), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidDisputeStatusTransition, result.Error!.Message);
        Assert.Equal(DisputeStatus.UnderReview, dispute.Status);
        Assert.Null(dispute.RefundAmount);
        Assert.Null(dispute.ResolvedBy);
        Assert.Null(dispute.ResolvedOn);
        Assert.Null(dispute.ResolutionNotes);
    }

    [Fact]
    public async Task Missing_dispute_returns_not_found()
    {
        _disputeRepository
            .Setup(r => r.GetForUpdateAsync(DisputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dispute?)null);

        var result = await CreateHandler().Handle(
            new UpdateDisputeStatus.Command(DisputeId, DisputeStatus.Closed), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeNotFound, result.Error!.Message);
    }
}
