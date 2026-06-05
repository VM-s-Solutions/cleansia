using Cleansia.Core.AppServices.Features.DataRetention;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class DataRetentionTimerHandler(
    IDataRetentionBackgroundService dataRetentionService,
    ILogger<DataRetentionTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("DataRetentionCleanup timer triggered at {Time}", DateTime.UtcNow);
        await dataRetentionService.RunAllRetentionTasksAsync(ct);
        logger.LogInformation("DataRetentionCleanup completed");
    }
}
