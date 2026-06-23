using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Behaviors;

/// <summary>
/// ADR-0012 D2/D2.1/D2.2/D3/D5 — the admin-action capture engine. Registered INNER to
/// <c>UnitOfWorkPipelineBehavior</c> so its <c>next()</c> (the handler) returns before the UoW commit
/// fires: the success-audit row is added to the SAME scoped DbContext and rides the single
/// <c>SaveChangesAsync</c> the UoW commits — atomic with the action (a rolled-back action leaves no
/// row; a failed audit insert rolls the action back).
///
/// <para>Gate (D3): audited iff the request type name ends <c>Command</c> AND the caller carries the
/// Administrator role claim — the only host-independent admin discriminator (precedent
/// <c>AddDisputeMessage</c>). Queries and non-admin mutations produce no row.</para>
///
/// <para>Failures (D2.1/D2.2): a business-failure the handler returns, or a thrown exception, means the
/// action transaction never commits, so the row is written OUT-OF-BAND via <see cref="IAuditFailureSink"/>
/// in its own committed scope. The sink is best-effort and swallowed — it NEVER changes the error returned
/// to the admin; the exception path writes the failure row then rethrows. Two failure shapes this inner
/// behavior structurally cannot see — a validation reject (short-circuited OUTER to it) and a commit-throw
/// (raised AFTER it has returned its success-add) — are owned by the outer
/// <c>AuditFailureCaptureBehavior</c>; the shared <see cref="IAuditContext"/> latch keeps a failure
/// recorded exactly once.</para>
/// </summary>
public class AuditLogBehavior<TRequest, TResponse>(
    IUserSessionProvider userSessionProvider,
    IAuditContext auditContext,
    IAuditWriter auditWriter,
    IAuditFailureSink auditFailureSink,
    AuditEntryFactory auditEntryFactory,
    ILogger<AuditLogBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var descriptor = AuditActionDescriptor.For(request.GetType());

        if (!AdminMutationGate.IsAuditable(request, descriptor, userSessionProvider))
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
            await RecordFailureOutOfBandAsync(request, descriptor, ex.GetType().Name, cancellationToken);
            throw;
        }

        if (response is BusinessResult result)
        {
            if (result.IsSuccess)
            {
                auditWriter.Add(auditEntryFactory.CreateSuccess(request, descriptor, auditContext.DrainSnapshot()));
            }
            else
            {
                await RecordFailureOutOfBandAsync(request, descriptor, result.Error?.Code, cancellationToken);
            }
        }

        return response;
    }

    private async Task RecordFailureOutOfBandAsync(
        TRequest request,
        AuditActionDescriptor descriptor,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        // The latch keeps the out-of-band failure row exactly once across the inner and outer audit
        // behaviors: if the outer AuditFailureCaptureBehavior already recorded this failure, skip.
        if (!auditContext.TryClaimFailureRecording())
        {
            return;
        }

        try
        {
            await auditFailureSink.RecordFailureAsync(
                auditEntryFactory.CreateFailure(request, descriptor, errorCode),
                cancellationToken);
        }
        catch (Exception sinkEx)
        {
            // D2.2: best-effort and swallowed — a failed *failure-audit* write must never convert into a
            // different error returned to the admin. A lost failure-record is a logged gap, not a 500.
            logger.LogError(
                sinkEx,
                "Out-of-band audit-failure write threw for action {Action}; the failed admin action was not recorded.",
                descriptor.Action);
        }
    }
}
