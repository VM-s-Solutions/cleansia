using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Behaviors;

/// <summary>
/// Puts a failed erasure on record. The erasure stages everything and commits once, in
/// <c>UnitOfWorkPipelineBehavior</c>, so the request row it staged as <c>Processing</c> is rolled back
/// together with the walk whenever that commit throws — the subject is untouched and nothing says an
/// erasure was ever attempted. Registered OUTER to the unit of work so it observes that throw, and any
/// refusal or exception after the walk began, and writes a <c>Failed</c> request row through the
/// out-of-band <see cref="IGdprDeletionFailureSink"/> in a scope of its own. A request in which no walk
/// began (a validation reject, a blocking-order refusal, any other command) passes through untouched.
/// Best-effort like the audit sink: a failure to record never changes the error returned to the caller.
/// </summary>
public class ErasureFailureCaptureBehavior<TRequest, TResponse>(
    IErasureAttempt erasureAttempt,
    IGdprDeletionFailureSink failureSink,
    ILogger<ErasureFailureCaptureBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const string UnkeyedFailure = "failure";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        TResponse response;
        try
        {
            response = await next(cancellationToken);
        }
        catch (Exception ex)
        {
            await RecordAsync(ErasureFailureNote.Describe(ex), cancellationToken);
            throw;
        }

        if (response is BusinessResult { IsFailure: true } result)
        {
            await RecordAsync(AuditErrorCode.Resolve(result) ?? UnkeyedFailure, cancellationToken);
        }

        return response;
    }

    private async Task RecordAsync(string note, CancellationToken cancellationToken)
    {
        if (!erasureAttempt.Started)
        {
            return;
        }

        try
        {
            await failureSink.RecordFailureAsync(
                erasureAttempt.SubjectUserId!,
                erasureAttempt.RequestId!,
                erasureAttempt.ProcessedBy!,
                note,
                cancellationToken);
        }
        catch (Exception sinkEx)
        {
            logger.LogError(
                sinkEx,
                "Out-of-band erasure-failure write threw for request {RequestId}; the failed erasure was not recorded.",
                erasureAttempt.RequestId);
        }
    }
}
