using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// Re-runs the erasure a failed deletion request describes. Dispatched by an admin from the request
/// list and, with no session behind it, by the daily retry sweep — the audit gate records the former on
/// the admin table and lets the latter through unrecorded, the request row being the timer's own record.
/// </summary>
[AuditAction("gdpr.user.delete.retry", Sensitive = true, ResourceType = "GdprRequest")]
public static class AdminRetryUserDeletion
{
    public record Command(string RequestId) : ICommand;

    /// <summary>
    /// A <c>Processing</c> row older than this cannot still be a live run — the walk takes seconds — so it
    /// is the trace of a host that died mid-erasure with nothing committed, and is retried like a failure.
    /// </summary>
    public static readonly TimeSpan StaleProcessingAfter = TimeSpan.FromMinutes(30);

    public static bool IsRetryable(GdprRequest request, DateTimeOffset now)
        => request.RequestType == GdprRequest.DeletionRequestType
           && (request.Status == GdprRequestStatus.Failed
               || (request.Status == GdprRequestStatus.Processing
                   && (request.UpdatedOn ?? request.CreatedOn) < now - StaleProcessingAfter));

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IGdprRequestRepository gdprRequestRepository)
        {
            RuleFor(c => c.RequestId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await gdprRequestRepository.GetQueryable().AnyAsync(r => r.Id == id, ct))
                .WithMessage(BusinessErrorMessage.GdprRequestNotFound)
                .MustAsync(async (id, ct) =>
                {
                    var request = await gdprRequestRepository.GetQueryable().AsNoTracking().SingleAsync(r => r.Id == id, ct);
                    return IsRetryable(request, DateTimeOffset.UtcNow);
                })
                .WithMessage(BusinessErrorMessage.GdprRequestNotRetryable);
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IGdprDeletionService gdprDeletionService,
        IAuditContext auditContext)
        : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var actor = userSessionProvider.GetUserEmail() ?? GdprAuditReasons.SystemActor;
            string? subjectUserId = null;

            var result = await gdprDeletionService.RetryDeletionAsync(
                request.RequestId,
                resolveAuditActor: user =>
                {
                    subjectUserId = user.Id;
                    return (actor, $"Retried by {actor}");
                },
                cancellationToken);

            if (result.IsSuccess)
            {
                var snapshot = new AdminDeleteUserAccount.GdprActionSnapshot(subjectUserId!, AdminDeleteUserAccount.DeletionScope);
                auditContext.RecordChange("GdprRequest", request.RequestId, snapshot, snapshot);
            }

            return result;
        }
    }
}
