using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Behaviors;

/// <summary>
/// ADR-0002 D1 — the post-commit dispatch step, realized as the <b>OUTERMOST</b> MediatR pipeline
/// behavior (registered before Validation and UnitOfWork). It fixes F2/SEC-W1: a queue message reaches
/// the wire <b>only after</b> the owning <c>UnitOfWork</c> commit succeeds, so a rolled-back write
/// can never leave a phantom side effect on the queue.
///
/// <para>Flow: <c>next()</c> runs the inner pipeline (Validation → UnitOfWork commit → Handler).
/// <list type="bullet">
///   <item>If the commit throws, <c>next()</c> propagates the exception, the dispatch guard below is
///   never reached, and the scoped <see cref="IPendingDispatch"/> buffer is discarded — NOTHING is
///   dispatched (AC2, the F2 fix; also closes the parallel-retry double-dispatch — Context §1a).</item>
///   <item>On a validation failure the handler never ran, so the buffer is empty — nothing dispatched.</item>
///   <item>ONLY on <c>response is BusinessResult { IsSuccess: true }</c> does it drain the buffer and
///   send each message exactly once, STRICTLY after the commit (AC1).</item>
/// </list></para>
///
/// <para>Dispatch failures are <b>logged and swallowed</b> — never converted into a 500. A
/// customer-facing operation that already committed must never fail because a downstream effect could
/// not be put on the wire (the fiscal-compliance invariant). A lost dispatch is the Wave-0 residual
/// gap (recovered durably in Wave-1; detected by reconciliation on the fiscal queues in Wave-0).</para>
/// </summary>
public class PostCommitDispatchBehavior<TRequest, TResponse>(
    IPendingDispatch pending,
    IQueueClient queueClient,
    ILogger<PostCommitDispatchBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validation → UnitOfWork (commit) → Handler all run inside next(). If the commit throws,
        // next() throws → the guard below is never reached → no dispatch, buffer discarded with scope.
        var response = await next(cancellationToken);

        // Dispatch ONLY on a committed success (predicate aligned with UnitOfWorkPipelineBehavior — C7).
        if (response is BusinessResult { IsSuccess: true })
        {
            await DispatchAsync(cancellationToken);
        }

        return response;
    }

    private async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var messages = pending.Drain();
        foreach (var message in messages)
        {
            try
            {
                // The body is the already-serialized QueueEnvelope<T>; the queue client sends a string
                // verbatim (no re-serialization). Best-effort: one failure never blocks the others.
                await queueClient.SendAsync(message.QueueName, message.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                // Logged and swallowed — NEVER a 500 (D1). The committed operation already succeeded.
                logger.LogError(
                    ex,
                    "Post-commit dispatch failed for queue {Queue} (messageKey {MessageKey}); the operation committed " +
                    "but the downstream effect was not enqueued. Wave-1 outbox / Wave-0 reconciliation is the backstop.",
                    message.QueueName,
                    message.MessageKey);
            }
        }
    }
}
