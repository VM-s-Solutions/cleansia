namespace Cleansia.Core.AppServices.Services.Interfaces;

public interface IPeriodReminderBackgroundService
{
    Task SendPeriodEndRemindersAsync(CancellationToken cancellationToken = default);
}
