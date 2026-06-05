using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// ADR-0002 D3 (F3) — poison consumer for <c>calculate-order-pay</c>. Logs+alerts+stores the poisoned
/// pay-calc body (store at minimum) and acks. See <see cref="PoisonHandlerBase"/>.
/// </summary>
public sealed class CalculateOrderPayPoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<CalculateOrderPayPoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.CalculateOrderPay;
}
