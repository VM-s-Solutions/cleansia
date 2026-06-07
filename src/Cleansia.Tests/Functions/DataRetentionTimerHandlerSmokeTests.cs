using Cleansia.Core.AppServices.Features.DataRetention;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

public class DataRetentionTimerHandlerSmokeTests
{
    private readonly Mock<IDataRetentionBackgroundService> _dataRetentionService = new();

    private DataRetentionTimerHandler CreateHandler() => new(
        _dataRetentionService.Object,
        NullLogger<DataRetentionTimerHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Runs_The_Retention_Tasks_Once()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _dataRetentionService.Verify(
            s => s.RunAllRetentionTasksAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
