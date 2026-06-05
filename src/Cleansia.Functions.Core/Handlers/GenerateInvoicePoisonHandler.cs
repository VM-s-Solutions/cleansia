using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for the FISCAL queue <c>generate-invoice</c>. Persists the
/// durable <c>DeadLetter</c> row (AC3) + alerts + acks. See <see cref="PoisonHandlerBase"/>.
/// </summary>
public sealed class GenerateInvoicePoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<GenerateInvoicePoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.GenerateInvoice;
}
