using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auth;

public class ChangeOwnPassword
{
    public record Command(
        string CurrentPassword,
        string NewPassword) : ICommand<Response>;

    public record Response(string Id);

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CurrentPassword)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(x => x.NewPassword).ValidatePassword();
        }
    }

    internal class Handler(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            // [OWN-DATA] (S1/S3): the subject is always the JWT caller — the command carries no user id.
            var userId = userSessionProvider.GetUserId()!;
            var now = DateTimeOffset.UtcNow;
            var user = await userRepository.GetByIdAsync(userId, cancellationToken);

            if (user?.Password is null)
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.CurrentPassword), BusinessErrorMessage.CurrentPasswordInvalid));
            }

            // Lockout gate precedes the password compare so an exhausted budget refuses without
            // evaluating the current password — no guessing oracle. The charge below shares the login
            // lockout pair (atomic conditional UPDATE in the repo), so this failure never reaches the
            // unit-of-work commit yet the counter still lands.
            if (user.IsLockedOut(now))
            {
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.CurrentPassword), BusinessErrorMessage.AccountLocked));
            }

            if (!command.CurrentPassword.CheckIfPasswordSame(user.Password))
            {
                await userRepository.RecordFailedCurrentPasswordAttemptAsync(userId, now, cancellationToken);
                return BusinessResult.Failure<Response>(
                    new Error(nameof(command.CurrentPassword), BusinessErrorMessage.CurrentPasswordInvalid));
            }

            user.ResetLoginThrottle();

            // Raw password on the entity — the EF PasswordConverter hashes exactly once on persist.
            user.UpdatePassword(command.NewPassword);

            return BusinessResult.Success(new Response(user.Id));
        }
    }
}
