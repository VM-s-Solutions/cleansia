using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0121 / ADR-0002 D5 step 1 — thin trigger shell; body lives in SendSitewidePromoFanoutHandler (Core).
public class SendSitewidePromoFanoutFunction(SendSitewidePromoFanoutHandler handler)
{
    [Function("SendSitewidePromoFanout")]
    public Task Run(
        [QueueTrigger("sitewide-promo-fanout", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
