using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class DeactivateAdminUser
{
    [AuditAction("admin.user.deactivate", ResourceType = "AdminUser")]
    public record Command(string UserId) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IUserSessionProvider userSessionProvider)
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (userId, ct) =>
                    await userRepository.GetAll()
                        .AnyAsync(u => u.Id == userId && u.Profile == UserProfile.Administrator, ct))
                .WithMessage(BusinessErrorMessage.AdminUserNotFound)
                // Never deactivate the last ACTIVE Administrator-role administrator — a company with only
                // a Support or an Accountant left has nobody who can assign a role or create an account,
                // so the console is locked with no recovery. Reject when the target is the only one.
                .MustAsync(async (userId, ct) =>
                    await userRepository.GetAll()
                        .CountAsync(u => u.Profile == UserProfile.Administrator && u.IsActive
                            && u.AdminRole == AdminRole.Administrator
                            && u.Id != userId, ct) > 0)
                .WithMessage(BusinessErrorMessage.CannotDeactivateLastAdmin);

            RuleFor(x => x.UserId)
                .Must(userId => userId != userSessionProvider.GetUserId())
                .WithMessage(BusinessErrorMessage.CannotDeactivateSelf);
        }
    }

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        ITenantProvider tenantProvider)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var actorId = userSessionProvider.GetUserId() ?? string.Empty;
            var tenantId = tenantProvider.GetCurrentTenantId()!;

            // The validator's count-then-check is a fast-path message, not the guard: two concurrent
            // deactivations of the final two Administrators both pass it under READ COMMITTED. The
            // repository's write is the guard — one transaction under the company's advisory lock, the
            // deactivation landing only while another active Administrator remains; 0 rows means the
            // target is (now) the last one. It commits itself, before and apart from the pipeline's commit.
            var rowsAffected = await userRepository.DeactivateAdministratorIfAnotherRemainsAsync(
                tenantId, command.UserId, actorId, DateTimeOffset.UtcNow, cancellationToken);

            if (rowsAffected == 0)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(Command.UserId), BusinessErrorMessage.CannotDeactivateLastAdmin));
            }

            return BusinessResult.Success(new Response(command.UserId));
        }
    }
}
