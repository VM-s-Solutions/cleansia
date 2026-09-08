using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class PayPeriodTimerHandler(
    IPayPeriodBackgroundService payPeriodService,
    IPayPeriodClosingConfig config,
    ILogger<PayPeriodTimerHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("CloseExpiredPayPeriods timer triggered at {Time}", DateTime.UtcNow);

        // The switch is here rather than inside the service because EnsureOpenPeriodAsync is called
        // inline by pay calculation and must keep working; only the scheduled sweep — which generates
        // and emails an invoice per employee — is switchable. -> IPayPeriodClosingConfig
        if (!config.Enabled)
        {
            logger.LogWarning(
                "CloseExpiredPayPeriods disabled by configuration (PayPeriodClosing:Enabled). Skipping");
            return;
        }

        await payPeriodService.CloseExpiredPeriodsAndOpenNewAsync(ct);
        logger.LogInformation("CloseExpiredPayPeriods completed");
    }
}
