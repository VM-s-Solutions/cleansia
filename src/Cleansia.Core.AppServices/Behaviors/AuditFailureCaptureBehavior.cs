using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Behaviors;

/// <summary>
/// ADR-0012 D2.1/D2.2 — the OUTERMOST audit step, the backstop for the two failed-action shapes the
/// inner <c>AuditLogBehavior</c> structurally cannot see:
/// <list type="number">
///   <item>a <b>validation reject</b> — <c>ValidationPipelineBehavior</c> returns the failure result
///   WITHOUT calling <c>next()</c>, so neither <c>UnitOfWorkPipelineBehavior</c> nor the inner
///   <c>AuditLogBehavior</c> ever runs;</item>
///   <item>a <b>commit-throw</b> — the inner <c>AuditLogBehavior</c> adds its success row and returns
///   BEFORE the outer <c>UnitOfWorkPipelineBehavior.CommitAsync</c> runs, so a throw from that single
///   <c>SaveChangesAsync</c> escapes the inner behavior's already-exited try/catch.</item>
/// </list>
/// Registered OUTER to <c>PostCommitDispatchBehavior</c> so it observes the final outcome of the whole
/// inner pipeline — the short-circuited validation result and the propagated commit exception both reach
/// it. It routes a failed mutation to the OUT-OF-BAND <see cref="IAuditFailureSink"/> exactly like
/// the inner behavior does — through the same <see cref="AuditGate"/>, to the same table — sharing the
/// per-request <see cref="IAuditContext"/> latch so a failure the inner behavior already recorded (a
/// handler-returned business failure) is not double-written. It is best-effort and swallowed: a sink
/// failure never changes the error returned to the caller; the exception path writes the failure row
/// then rethrows. The recorded <c>ErrorCode</c> is the first validation failure's key
/// (<see cref="AuditErrorCode"/>), not the <c>ValidationError</c> sentinel.
/// </summary>
public class AuditFailureCaptureBehavior<TRequest, TResponse>(
    IUserSessionProvider userSessionProvider,
    IAuditContext auditContext,
    IAuditFailureSink auditFailureSink,
    AuditEntryFactory auditEntryFactory,
    ILogger<AuditFailureCaptureBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var descriptor = AuditActionDescriptor.For(request.GetType());

        var audience = AuditGate.Resolve(request, descriptor, userSessionProvider);
        if (audience is null)
        {
            return await next(cancellationToken);
        }

        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch (Exception ex)
        {
            await RecordFailureOutOfBandAsync(request, descriptor, audience.Value, ex.GetType().Name, cancellationToken);
            throw;
        }

        if (response is BusinessResult { IsFailure: true } result)
        {
            await RecordFailureOutOfBandAsync(request, descriptor, audience.Value, AuditErrorCode.Resolve(result), cancellationToken);
        }

        return response;
    }

    private async Task RecordFailureOutOfBandAsync(
        TRequest request,
        AuditActionDescriptor descriptor,
        AuditAudience audience,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        // The inner AuditLogBehavior owns any failure it could see (a handler-returned business failure);
        // it claims the latch first, so this outer backstop only fires for a validation reject (the inner
        // behavior never ran) or a commit-throw (the inner behavior returned a success before the throw).
        if (!auditContext.TryClaimFailureRecording())
        {
            return;
        }

        try
        {
            if (audience == AuditAudience.Admin)
            {
                await auditFailureSink.RecordFailureAsync(
                    auditEntryFactory.CreateFailure(request, descriptor, errorCode),
                    cancellationToken);
            }
            else
            {
                await auditFailureSink.RecordFailureAsync(
                    auditEntryFactory.CreateCustomerFailure(request, descriptor, errorCode),
                    cancellationToken);
            }
        }
        catch (Exception sinkEx)
        {
            logger.LogError(
                sinkEx,
                "Out-of-band audit-failure write threw for action {Action} ({Audience}); the failed action was not recorded.",
                descriptor.Action,
                audience);
        }
    }
}
