using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.AdminUsers;

public class DeactivateAdminUser
{
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
                // T-0107 (IDA-SEC-08): never deactivate the last ACTIVE administrator — that would
                // lock the tenant out of its own admin console with no recovery. Reject when the
                // target is the only active admin (the active-admin count would drop to 0).
                .MustAsync(async (userId, ct) =>
                    await userRepository.GetAll()
                        .CountAsync(u => u.Profile == UserProfile.Administrator && u.IsActive
                            && u.Id != userId, ct) > 0)
                .WithMessage(BusinessErrorMessage.CannotDeactivateLastAdmin);

            RuleFor(x => x.UserId)
                .Must(userId => userId != userSessionProvider.GetUserId())
                .WithMessage(BusinessErrorMessage.CannotDeactivateSelf);
        }
    }

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var actorId = userSessionProvider.GetUserId() ?? string.Empty;

            // PR review #22 (S7a) — ATOMIC last-active-admin guard. The validator's count-then-check is a
            // fast-path UX message but is NOT race-safe: two concurrent deactivations of the final two
            // admins can both pass the validator under READ COMMITTED and zero out active admins. This
            // single conditional UPDATE deactivates the target ONLY while another ACTIVE admin still
            // exists; 0 rows affected ⇒ the target is (now) the last active admin ⇒ CannotDeactivateLastAdmin.
            var now = DateTimeOffset.UtcNow;
            var rowsAffected = await userRepository.GetAll()
                .Where(u => u.Id == command.UserId
                    && u.Profile == UserProfile.Administrator
                    && u.IsActive
                    && userRepository.GetAll().Any(other =>
                        other.Profile == UserProfile.Administrator
                        && other.IsActive
                        && other.Id != command.UserId))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(u => u.IsActive, false)
                        .SetProperty(u => u.DeactivatedBy, actorId)
                        .SetProperty(u => u.DeactivatedOn, now),
                    cancellationToken);

            if (rowsAffected == 0)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(Command.UserId), BusinessErrorMessage.CannotDeactivateLastAdmin));
            }

            return BusinessResult.Success(new Response(command.UserId));
        }
    }
}
