using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class SetAdminRole
{
    [AuditAction("admin.user.set_role", ResourceType = "AdminUser")]
    public record Command(string UserId, AdminRole Role) : ICommand<Response>;

    public record Response(string Id, AdminRole Role);

    public record RoleSnapshot(string UserId, AdminRole? Role);

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
                .Must(userId => userId != userSessionProvider.GetUserId())
                .WithMessage(BusinessErrorMessage.CannotChangeOwnRole);

            RuleFor(x => x.Role)
                .IsInEnum()
                .WithMessage(BusinessErrorMessage.InvalidEnumValue);
        }
    }

    public class Handler(
        IUserRepository userRepository,
        ITenantProvider tenantProvider,
        IAuditContext auditContext)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var tenantId = tenantProvider.GetCurrentTenantId()!;
            var before = await userRepository.GetAll()
                .AsNoTracking()
                .Where(u => u.Id == command.UserId && u.Profile == UserProfile.Administrator)
                .Select(u => u.AdminRole)
                .FirstOrDefaultAsync(cancellationToken);

            var rowsAffected = await userRepository.DemoteAdministratorIfAnotherRemainsAsync(
                tenantId, command.UserId, command.Role, cancellationToken);

            if (rowsAffected == 0)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(Command.UserId), BusinessErrorMessage.CannotDemoteLastAdministrator));
            }

            auditContext.RecordChange(
                "AdminUser",
                command.UserId,
                new RoleSnapshot(command.UserId, before),
                new RoleSnapshot(command.UserId, command.Role));

            return BusinessResult.Success(new Response(command.UserId, command.Role));
        }
    }
}
