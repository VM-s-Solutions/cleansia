using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

public class PeriodReminderTimerFunction(
    IPeriodReminderBackgroundService reminderService,
    ILogger<PeriodReminderTimerFunction> logger)
{
    [Function("SendPeriodEndReminders")]
    public async Task Run([TimerTrigger("0 0 9 * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("SendPeriodEndReminders timer triggered at {Time}", DateTime.UtcNow);
        await reminderService.SendPeriodEndRemindersAsync(ct);
        logger.LogInformation("SendPeriodEndReminders completed");
    }
}
