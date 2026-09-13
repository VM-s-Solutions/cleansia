using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Gdpr;

/// <summary>
/// An admin dumping another person's whole record is a PII egress, so it is a Command and audited — the
/// <c>RevealOrderAccessInstructions</c> precedent. As a Query it left no record at all: the
/// <c>GdprRequest("Export")</c> row it added had no commit to ride and the audit gate never saw it
/// (ADR-0062 D6). A build that throws commits nothing; the pipeline records the failure out-of-band.
/// </summary>
[AuditAction("gdpr.user.export", Sensitive = true, ResourceType = "User")]
public static class AdminExportUserData
{
    public record Command(string UserId) : ICommand<GdprExportDto>;

    // ADR-0012 D4.1 — the subject id, the scope and row counts ONLY. The exported data is exactly what
    // the audit row must never copy.
    public record GdprExportSnapshot(string SubjectUserId, string Scope, int OrderCount, int CustomerActionCount);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IUserRepository userRepository)
        {
            // The GDPR export tool is for customer/employee data-subject
            // requests only — it must never target an administrator. Cascade.Stop so the existence
            // check runs before the Profile guard and BuildAsync (which marks a completed export
            // row) is never reached on a reject.
            RuleFor(q => q.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId)
                .MustAsync(async (id, ct) =>
                    !await userRepository.GetAll()
                        .AnyAsync(u => u.Id == id && u.Profile == UserProfile.Administrator, ct))
                .WithMessage(BusinessErrorMessage.CannotTargetAdminViaGdprTool);
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IGdprExportService gdprExportService,
        IGdprRequestRepository gdprRequestRepository,
        IAuditContext auditContext)
        : ICommandHandler<Command, GdprExportDto>
    {
        private const string ExportScope = "Export";

        public async Task<BusinessResult<GdprExportDto>> Handle(Command request, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail() ?? GdprAuditReasons.FallbackAdminActor;
            var exportedBy = $"admin:{adminEmail}";

            var auditEntry = Core.Domain.Users.GdprRequest.Create(request.UserId, ExportScope);
            gdprRequestRepository.Add(auditEntry);

            var export = await gdprExportService.BuildAsync(request.UserId, exportedBy, cancellationToken);
            auditEntry.MarkCompleted(adminEmail);

            var snapshot = new GdprExportSnapshot(request.UserId, ExportScope, export.Orders.Count, export.CustomerActions.Count);
            auditContext.RecordChange("User", request.UserId, snapshot, snapshot);

            return BusinessResult.Success(export);
        }
    }
}
