using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for <c>company-wind-down</c>. Records the durable dead-letter
/// row, alerts and acks; the admin page's "Run wind-down again" is the designed recovery.
/// </summary>
public sealed class CompanyWindDownPoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<CompanyWindDownPoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.CompanyWindDown;
}
