using Cleansia.Core.AppServices.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

public class RetryFailedFiscalRegistrationsHandler(
    IFiscalRetryService fiscalRetryService,
    ILogger<RetryFailedFiscalRegistrationsHandler> logger)
{
    public async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("RetryFailedFiscalRegistrations timer triggered at {Time}", DateTime.UtcNow);
        var processed = await fiscalRetryService.ProcessDueRetriesAsync(ct);
        logger.LogInformation("RetryFailedFiscalRegistrations processed {Count} receipts", processed);
    }
}
