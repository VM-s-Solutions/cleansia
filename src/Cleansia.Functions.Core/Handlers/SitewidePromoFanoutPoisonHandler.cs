using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for <c>sitewide-promo-fanout</c>. Logs+alerts+stores the
/// poisoned campaign body (store at minimum) and acks. See <see cref="PoisonHandlerBase"/>.
/// </summary>
public sealed class SitewidePromoFanoutPoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<SitewidePromoFanoutPoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.SitewidePromoFanout;
}
