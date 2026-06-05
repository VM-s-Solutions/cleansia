using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class PeriodReminderTimerHandler(
    IPeriodReminderBackgroundService reminderService,
    ILogger<PeriodReminderTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("SendPeriodEndReminders timer triggered at {Time}", DateTime.UtcNow);
        await reminderService.SendPeriodEndRemindersAsync(ct);
        logger.LogInformation("SendPeriodEndReminders completed");
    }
}
