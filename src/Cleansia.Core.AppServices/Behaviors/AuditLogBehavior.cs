using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Behaviors;

/// <summary>
/// ADR-0012 D2/D2.1/D2.2/D3/D5 — the action capture engine. Registered INNER to
/// <c>UnitOfWorkPipelineBehavior</c> so its <c>next()</c> (the handler) returns before the UoW commit
/// fires: the success-audit row is added to the SAME scoped DbContext and rides the single
/// <c>SaveChangesAsync</c> the UoW commits — atomic with the action (a rolled-back action leaves no
/// row; a failed audit insert rolls the action back).
///
/// <para>Gate: <see cref="AuditGate"/> answers WHICH table, or none. The admin arm (D3) takes every
/// admin Command — and, where an admin marker allows it and the host is the admin host, an anonymous
/// caller (the admin sign-in); the customer arm (ADR-0062 D1) takes a Command whose marker opted it in,
/// from a Customer or — where the marker allows it and the host is a customer host — an anonymous caller.
/// Queries, employee mutations and unmarked customer commands produce no row. A handler that took a
/// branch its marker does not describe declines the success row through <see cref="IAuditContext"/>;
/// its refusals are still recorded.</para>
///
/// <para>Failures (D2.1/D2.2): a business-failure the handler returns, or a thrown exception, means the
/// action transaction never commits, so the row is written OUT-OF-BAND via <see cref="IAuditFailureSink"/>
/// in its own committed scope. The sink is best-effort and swallowed — it NEVER changes the error returned
/// to the caller; the exception path writes the failure row then rethrows. Two failure shapes this inner
/// behavior structurally cannot see — a validation reject (short-circuited OUTER to it) and a commit-throw
/// (raised AFTER it has returned its success-add) — are owned by the outer
/// <c>AuditFailureCaptureBehavior</c>; the shared <see cref="IAuditContext"/> latch keeps a failure
/// recorded exactly once. The failure row's <c>ErrorCode</c> is the refusal key
/// (<see cref="AuditErrorCode"/>), on both arms.</para>
/// </summary>
public class AuditLogBehavior<TRequest, TResponse>(
    IUserSessionProvider userSessionProvider,
    IHostAudienceProvider hostAudienceProvider,
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

        var audience = AuditGate.Resolve(request, descriptor, userSessionProvider, hostAudienceProvider);
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

        if (response is BusinessResult result)
        {
            if (result.IsSuccess)
            {
                if (auditContext.SuccessRowDeclined)
                {
                    return response;
                }

                var snapshot = auditContext.DrainSnapshot();
                if (audience == AuditAudience.Admin)
                {
                    auditWriter.Add(auditEntryFactory.CreateSuccess(request, descriptor, snapshot));
                }
                else
                {
                    auditWriter.Add(auditEntryFactory.CreateCustomerSuccess(request, descriptor, snapshot));
                }
            }
            else
            {
                await RecordFailureOutOfBandAsync(request, descriptor, audience.Value, AuditErrorCode.Resolve(result), cancellationToken);
            }
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
        // The latch keeps the out-of-band failure row exactly once across the inner and outer audit
        // behaviors: if the outer AuditFailureCaptureBehavior already recorded this failure, skip.
        if (!auditContext.TryClaimFailureRecording())
        {
            return;
        }

        try
        {
            var snapshot = auditContext.DrainSnapshot();
            if (audience == AuditAudience.Admin)
            {
                await auditFailureSink.RecordFailureAsync(
                    auditEntryFactory.CreateFailure(request, descriptor, errorCode, snapshot),
                    cancellationToken);
            }
            else
            {
                await auditFailureSink.RecordFailureAsync(
                    auditEntryFactory.CreateCustomerFailure(request, descriptor, errorCode, snapshot),
                    cancellationToken);
            }
        }
        catch (Exception sinkEx)
        {
            // D2.2: best-effort and swallowed — a failed *failure-audit* write must never convert into a
            // different error returned to the caller. A lost failure-record is a logged gap, not a 500.
            logger.LogError(
                sinkEx,
                "Out-of-band audit-failure write threw for action {Action} ({Audience}); the failed action was not recorded.",
                descriptor.Action,
                audience);
        }
    }
}
