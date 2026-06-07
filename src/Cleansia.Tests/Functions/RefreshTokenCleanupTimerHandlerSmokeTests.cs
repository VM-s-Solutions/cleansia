using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class RefreshTokenCleanupTimerHandlerSmokeTests
{
    private readonly Mock<IRefreshTokenCleanupService> _cleanupService = new();

    private RefreshTokenCleanupTimerHandler CreateHandler() => new(
        _cleanupService.Object,
        NullLogger<RefreshTokenCleanupTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Runs_Cleanup_Once()
    {
        _cleanupService
            .Setup(s => s.CleanupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _cleanupService.Verify(
            s => s.CleanupAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
