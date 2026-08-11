using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class CleanupStalePendingOrdersHandlerSmokeTests
{
    private readonly Mock<IMediator> _mediator = new();

    private CleanupStalePendingOrdersHandler CreateHandler() => new(
        _mediator.Object,
        NullLogger<CleanupStalePendingOrdersHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Drives_The_Sweep_Once()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new CleanupStalePendingOrders.Response(CancelledCount: 0)));
        // The same timer carries a SECOND command: membership benefit orphans live in
        // MembershipBenefitUsages and are invisible to the order sweep above (ADR-0035 AM-7).
        _mediator
            .Setup(m => m.Send(
                It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new ReleaseOrphanedBenefitReservations.Response(0)));

        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<CleanupStalePendingOrders.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mediator.Verify(
            m => m.Send(
                It.IsAny<ReleaseOrphanedBenefitReservations.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
