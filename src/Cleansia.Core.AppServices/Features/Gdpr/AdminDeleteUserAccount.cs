using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;


namespace Cleansia.Core.AppServices.Features.Gdpr;

public static class AdminDeleteUserAccount
{
    public record Command(string UserId) : ICommand;

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider)
        {
            // T-0107 (IDA-SEC-08): the GDPR delete tool is for customer/employee data-subject
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

    internal class Handler(
        IUserSessionProvider userSessionProvider,
        IGdprDeletionService gdprDeletionService)
        : ICommandHandler<Command>
    {
        public Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var adminEmail = userSessionProvider.GetUserEmail() ?? "admin";

            return gdprDeletionService.DeleteUserAccountAsync(
                request.UserId,
                deactivationReason: "GDPR_ADMIN_DELETION",
                resolveAuditActor: _ => (adminEmail, $"Admin deletion by {adminEmail}"),
                cancellationToken);
        }
    }
}
