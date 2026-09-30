using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-09-28: the cleaner an administrator takes off a job is told why. The reason is kept
/// on the reassignment's audit row, never in the notice, and the removed cleaner reads it here — nobody
/// else does, so a cleaner cannot read why a colleague was removed.
/// </summary>
public sealed class GetMyAssignmentRemovalTests
{
    private const string OrderId = "order-removal-1";
    private const string RemovedEmployeeId = "emp-removed";
    private const string ColleagueEmployeeId = "emp-colleague";

    private readonly Mock<IOrderAccessService> _orderAccess = new();
    private readonly Mock<IAdminActionAuditRepository> _audits = new();

    private static readonly string ReassignAction = AuditActionDescriptor.For(typeof(AdminReassignOrder.Command)).AdminAction;

    public GetMyAssignmentRemovalTests()
    {
        _audits
            .Setup(r => r.GetSucceededForResourceAsync(ReassignAction, "Order", OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Removal(ColleagueEmployeeId, "The colleague was late twice.", DateTimeOffset.UtcNow.AddHours(-1)),
                Removal(RemovedEmployeeId, "The customer asked for another cleaner.", DateTimeOffset.UtcNow.AddHours(-2)),
                Removal(RemovedEmployeeId, "An earlier removal.", DateTimeOffset.UtcNow.AddDays(-3)),
            ]);
    }

    [Fact]
    public async Task The_Removed_Cleaner_Reads_The_Reason_Of_Their_Latest_Removal()
    {
        Caller(RemovedEmployeeId);

        var validation = await new GetMyAssignmentRemoval.Validator(_orderAccess.Object, _audits.Object)
            .ValidateAsync(new GetMyAssignmentRemoval.Query(OrderId));
        var result = await new GetMyAssignmentRemoval.Handler(_orderAccess.Object, _audits.Object)
            .Handle(new GetMyAssignmentRemoval.Query(OrderId), CancellationToken.None);

        Assert.True(validation.IsValid);
        Assert.True(result.IsSuccess);
        Assert.Equal("The customer asked for another cleaner.", result.Value.Reason);
        Assert.Equal(OrderId, result.Value.OrderId);
    }

    [Theory]
    [InlineData("emp-never-removed")]
    [InlineData(null)]
    public async Task Anyone_Else_Is_Answered_Order_Not_Found(string? callerEmployeeId)
    {
        Caller(callerEmployeeId);

        var validation = await new GetMyAssignmentRemoval.Validator(_orderAccess.Object, _audits.Object)
            .ValidateAsync(new GetMyAssignmentRemoval.Query(OrderId));

        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(validation.Errors).ErrorMessage);
    }

    private void Caller(string? employeeId) =>
        _orderAccess.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);

    // Serialised the way the reassignment writes it, so a change to the snapshot's shape reaches here.
    private static AdminActionAudit Removal(string removedEmployeeId, string reason, DateTimeOffset occurredOn)
    {
        var context = new AuditContext();
        context.RecordChange(
            "Order",
            OrderId,
            new AdminReassignOrder.CrewSnapshot(OrderId, removedEmployeeId),
            new AdminReassignOrder.CrewSnapshot(OrderId, "emp-replacement"),
            reason);
        var snapshot = context.DrainSnapshot()!;

        return new AdminActionAudit
        {
            ActorId = "admin-user",
            Action = ReassignAction,
            ResourceType = snapshot.ResourceType,
            ResourceId = snapshot.ResourceId,
            Success = true,
            OccurredOn = occurredOn,
            Reason = snapshot.Reason,
            BeforeJson = snapshot.BeforeJson,
            AfterJson = snapshot.AfterJson,
        };
    }
}
