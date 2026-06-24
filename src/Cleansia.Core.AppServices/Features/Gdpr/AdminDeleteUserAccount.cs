using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;


namespace Cleansia.Core.AppServices.Features.Gdpr;

[AuditAction("gdpr.user.delete", Sensitive = true, ResourceType = "User")]
public static class AdminDeleteUserAccount
{
    public record Command(string UserId) : ICommand;

    // ADR-0012 D4.1 — scope + subject id ONLY. NEVER the subject's personal data, so the
    // accountability row is lawful to retain past the subject's erasure (it holds the actor's
    // identity + the subject's id + the scope, not the erased PII).
    public record GdprActionSnapshot(string SubjectUserId, string Scope);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider)
        {
            // The GDPR delete tool is for customer/employee data-subject
            // requests only. It must never anonymize an administrator or the caller themselves —
            // admins are managed exclusively through the AdminUsers feature. Cascade.Stop so the
            // existence check runs before the Profile/self guards and DeleteUserAccountAsync is
            // never reached on a reject.
            RuleFor(c => c.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await userRepository.ExistsAsync(id, ct))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithId)
                .Must(id => id != userSessionProvider.GetUserId())
                .WithMessage(BusinessErrorMessage.CannotDeleteSelf)
                .MustAsync(async (id, ct) =>
                    !await userRepository.GetAll()
                        .AnyAsync(u => u.Id == id && u.Profile == UserProfile.Administrator, ct))
                .WithMessage(BusinessErrorMessage.CannotTargetAdminViaGdprTool);
        }
    }

    public class Handler(
        IUserSessionProvider userSessionProvider,
        IGdprDeletionService gdprDeletionService,
        IAuditContext auditContext)
        : ICommandHandler<Command>
    {
        private const string DeletionScope = "Deletion";

        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail() ?? GdprAuditReasons.FallbackAdminActor;

            var result = await gdprDeletionService.DeleteUserAccountAsync(
                request.UserId,
                deactivationReason: GdprAuditReasons.AdminDeletion,
                resolveAuditActor: _ => (adminEmail, $"Admin deletion by {adminEmail}"),
                cancellationToken);

            if (result.IsSuccess)
            {
                var snapshot = new GdprActionSnapshot(request.UserId, DeletionScope);
                auditContext.RecordChange("User", request.UserId, snapshot, snapshot);
            }

            return result;
        }
    }
}
