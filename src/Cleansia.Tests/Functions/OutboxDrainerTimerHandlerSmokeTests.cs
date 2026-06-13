using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// AC1 — the single outbox drainer was the one Function entry point with no handler-body test (only the
/// underlying IOutboxDrainerService is tested in Dispatch/). The [TimerTrigger] shell delegates to this
/// Core body; these pin that one drain tick runs per wakeup and that a zero-dispatch tick is a clean
/// no-op (nothing-due path).
/// </summary>
public class OutboxDrainerTimerHandlerSmokeTests
{
    private readonly Mock<IOutboxDrainerService> _drainerService = new();

    private OutboxDrainerTimerHandler CreateHandler() => new(
        _drainerService.Object,
        NullLogger<OutboxDrainerTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Drains_Once_Per_Tick()
    {
        _drainerService
            .Setup(s => s.DrainOnceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        await CreateHandler().HandleAsync(CancellationToken.None);

        _drainerService.Verify(s => s.DrainOnceAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Nothing_Due_Zero_Dispatched_Is_A_Clean_No_Op()
    {
        _drainerService
            .Setup(s => s.DrainOnceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var ex = await Record.ExceptionAsync(() => CreateHandler().HandleAsync(CancellationToken.None));

        Assert.Null(ex);
        _drainerService.Verify(s => s.DrainOnceAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Drain_Fault_Propagates_So_The_Next_Tick_Retries()
    {
        _drainerService
            .Setup(s => s.DrainOnceAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("dispatch store unreachable"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(CancellationToken.None));
    }
}
