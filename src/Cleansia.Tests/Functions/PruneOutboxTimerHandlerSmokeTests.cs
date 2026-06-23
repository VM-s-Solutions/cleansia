using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The [TimerTrigger] shell delegates to this Core body; these pin that one prune sweep runs per wakeup,
/// that a zero-prune tick is a clean no-op, and that a fault propagates so the next tick retries.
/// </summary>
public class PruneOutboxTimerHandlerSmokeTests
{
    private readonly Mock<IMediator> _mediator = new();

    private PruneOutboxTimerHandler CreateHandler() => new(
        _mediator.Object,
        NullLogger<PruneOutboxTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Drives_The_Prune_Once()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<PruneOutbox.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new PruneOutbox.Response(PrunedOutboxCount: 5, PrunedProcessedCount: 2)));

        await CreateHandler().HandleAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<PruneOutbox.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Nothing_To_Prune_Is_A_Clean_No_Op()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<PruneOutbox.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new PruneOutbox.Response(PrunedOutboxCount: 0, PrunedProcessedCount: 0)));

        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(CancellationToken.None));

        Assert.Null(ex);
        _mediator.Verify(
            m => m.Send(It.IsAny<PruneOutbox.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Prune_Fault_Propagates_So_The_Next_Tick_Retries()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<PruneOutbox.Command>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unreachable"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(CancellationToken.None));
    }
}
