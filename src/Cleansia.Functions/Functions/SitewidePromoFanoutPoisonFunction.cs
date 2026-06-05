using Cleansia.Functions.Core.Handlers;
using Microsoft.Azure.Functions.Worker;

namespace Cleansia.Functions.Functions;

// T-0120 / ADR-0002 D3 (F3) — thin -poison trigger shell; body lives in SitewidePromoFanoutPoisonHandler (Core).
public class SitewidePromoFanoutPoisonFunction(SitewidePromoFanoutPoisonHandler handler)
{
    [Function("SitewidePromoFanoutPoison")]
    public Task Run(
        [QueueTrigger("sitewide-promo-fanout-poison", Connection = "QueueStorageConnectionString")] string messageText,
        CancellationToken ct)
        => handler.HandleAsync(messageText, ct);
}
