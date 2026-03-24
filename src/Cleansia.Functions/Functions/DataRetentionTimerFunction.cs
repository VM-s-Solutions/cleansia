using Cleansia.Core.AppServices.Features.DataRetention;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

public class DataRetentionTimerFunction(
    IDataRetentionBackgroundService dataRetentionService,
    ILogger<DataRetentionTimerFunction> logger)
{
    [Function("DataRetentionCleanup")]
    public async Task Run([TimerTrigger("0 0 3 * * 0")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("DataRetentionCleanup timer triggered at {Time}", DateTime.UtcNow);
        await dataRetentionService.RunAllRetentionTasksAsync(ct);
        logger.LogInformation("DataRetentionCleanup completed");
    }
}
