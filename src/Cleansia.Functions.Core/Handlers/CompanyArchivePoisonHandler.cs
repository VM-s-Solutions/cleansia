using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for <c>company-archive</c>. Records the durable dead-letter
/// row, alerts and acks; the company stays frozen and un-archived, and the admin page's "Build
/// archive again" is the designed recovery.
/// </summary>
public sealed class CompanyArchivePoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<CompanyArchivePoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.CompanyArchive;
}
