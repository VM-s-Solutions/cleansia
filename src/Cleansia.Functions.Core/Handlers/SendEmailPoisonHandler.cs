using Cleansia.Core.Queue.Abstractions;
using Microsoft.Extensions.Logging;

namespace Cleansia.Functions.Core.Handlers;

/// <summary>
/// Poison consumer for <c>send-email</c>. Logs+alerts+stores the poisoned email body and acks. See
/// <see cref="PoisonHandlerBase"/>.
/// </summary>
public sealed class SendEmailPoisonHandler(
    IDeadLetterStore deadLetterStore,
    ILogger<SendEmailPoisonHandler> logger)
    : PoisonHandlerBase(deadLetterStore, logger)
{
    protected override string SourceQueue => QueueNames.SendEmail;
}
