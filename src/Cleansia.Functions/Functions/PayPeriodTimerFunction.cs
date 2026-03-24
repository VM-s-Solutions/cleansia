using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Functions;

public class PayPeriodTimerFunction(
    IPayPeriodBackgroundService payPeriodService,
    ILogger<PayPeriodTimerFunction> logger)
{
    [Function("CloseExpiredPayPeriods")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo timer, CancellationToken ct)
    {
        logger.LogInformation("CloseExpiredPayPeriods timer triggered at {Time}", DateTime.UtcNow);
        await payPeriodService.CloseExpiredPeriodsAndOpenNewAsync(ct);
        logger.LogInformation("CloseExpiredPayPeriods completed");
    }
}
