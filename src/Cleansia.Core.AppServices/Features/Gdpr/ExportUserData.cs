using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// A Command, not a Query, so the <c>GdprRequest("Export")</c> row it adds is committed — as a Query it
/// never was (ADR-0062 D6). Recorded as a customer act (owner ruling on Q-AUD-O3): the subject's whole
/// record leaving the platform is worth a row even when the subject is the one asking. A build that
/// throws commits nothing; the pipeline records the failure out-of-band with the exception type.
/// </summary>
[AuditAction("customer.gdpr.export", Audience = AuditAudience.Customer, ResourceType = "User")]
public static class ExportUserData
{
    public record Command : ICommand<GdprExportDto>;

    /// <summary>
    /// Section counts only. The export holds everything the platform knows about the subject, which is
    /// exactly what the audit row must never copy.
    /// </summary>
    public record GdprExportEvidence(int OrderCount, int DisputeCount, int ConsentCount, int CustomerActionCount, int WorkContractAcceptanceCount) : ICustomerAuditPayload;

    // Required even though the command is parameterless: the validation pipeline rejects any *Command
    // with no registered validator. The export operates on the session user, so there is no input.
    public class Validator : AbstractValidator<Command>;

    public class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IGdprExportService gdprExportService,
        IGdprRequestRepository gdprRequestRepository,
        IAuditContext auditContext)
        : ICommandHandler<Command, GdprExportDto>
    {
        public async Task<BusinessResult<GdprExportDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            // userId is non-null past the controller's [Permission] gate.
            var userId = userSessionProvider.GetUserId()!;
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);
            if (user is null)
                return BusinessResult.Failure<GdprExportDto>(new Error(
                    nameof(userId), BusinessErrorMessage.UserNotFound));

            var auditEntry = Core.Domain.Users.GdprRequest.Create(user.Id, GdprAuditReasons.ExportRequestType);
            gdprRequestRepository.Add(auditEntry);

            var export = await gdprExportService.BuildAsync(user.Id, user.Email, cancellationToken);
            auditEntry.MarkCompleted(GdprAuditReasons.SelfActor);

            auditContext.RecordEvidence("User", user.Id,
                new GdprExportEvidence(export.Orders.Count, export.Disputes.Count, export.Consents.Count, export.CustomerActions.Count, export.WorkContractAcceptances.Count));

            return BusinessResult.Success(export);
        }
    }
}
