using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for the FISCAL queue <c>generate-receipt</c>. A receipt that
/// fails 5× would otherwise be silently-lost fiscal work; this persists the durable
/// <c>DeadLetter</c> row (AC3) + alerts + acks. See <see cref="PoisonHandlerBase"/>.
/// </summary>
public sealed class GenerateReceiptPoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<GenerateReceiptPoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.GenerateReceipt;
}
