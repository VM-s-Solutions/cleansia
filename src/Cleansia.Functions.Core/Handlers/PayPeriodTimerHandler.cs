using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class PayPeriodTimerHandler(
    IPayPeriodBackgroundService payPeriodService,
    ILogger<PayPeriodTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("CloseExpiredPayPeriods timer triggered at {Time}", DateTime.UtcNow);
        await payPeriodService.CloseExpiredPeriodsAndOpenNewAsync(ct);
        logger.LogInformation("CloseExpiredPayPeriods completed");
    }
}
