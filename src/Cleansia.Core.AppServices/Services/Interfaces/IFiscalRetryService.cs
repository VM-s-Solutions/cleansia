namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Periodically retries fiscal registration for receipts whose initial attempt failed with a
/// transient error. Invoked by the <c>RetryFailedFiscalRegistrations</c> timer function.
/// </summary>
public interface IFiscalRetryService
{
    /// <summary>
    /// Processes a single batch of due-for-retry receipts. Returns the number of receipts
    /// processed (regardless of individual success/failure). Errors on individual receipts
    /// are logged and do not abort the batch.
    /// </summary>
    Task<int> ProcessDueRetriesAsync(CancellationToken cancellationToken);
}
