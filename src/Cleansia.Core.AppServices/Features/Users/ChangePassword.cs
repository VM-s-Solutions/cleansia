using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Users;

public class ChangePassword
{
    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;

        public Validator(IUserRepository userRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

            RuleFor(command => command.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Email))
                .MustAsync(userRepository.ExistsWithEmailIgnoringTenantAsync)
                .WithErrorCode(nameof(Command.Email))
                .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail);

            RuleFor(command => command.NewPassword).ValidatePassword();

            RuleFor(command => command.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required);

            RuleFor(command => command)
                .Cascade(CascadeMode.Stop)
                .MustAsync(HasAttemptBudgetAsync)
                    .WithErrorCode(nameof(Command.Code))
                    .WithMessage(BusinessErrorMessage.TooManyAttempts)
                .MustAsync(ValidateUserTokenAsync)
                    .WithErrorCode(nameof(Command.Code))
                    .WithMessage(BusinessErrorMessage.NotValidResetPasswordToken)
                .MustAsync(CheckIfPasswordDifferentAsync)
                    .WithErrorCode(nameof(Command.NewPassword))
                    .WithMessage(BusinessErrorMessage.SameResetPassword)
                .When(c => !string.IsNullOrWhiteSpace(c.Code) && !string.IsNullOrWhiteSpace(c.NewPassword))
                .WhenAsync((c, cc) => userRepository.ExistsWithEmailIgnoringTenantAsync(c.Email, cc));
        }

        // The reset command is email-bound, so every guess against an account holding an ACTIVE
        // reset code is charged to that account's per-code budget BEFORE the hash compare — a
        // wrong-code spray is stopped at the cap even if a later guess would have been correct
        // (ADR-0003 residual: per-code attempt cap). A fresh code re-grants the budget.
        private async Task<bool> HasAttemptBudgetAsync(Command command, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
            if (user?.ResetPasswordCode is null)
            {
                return true;
            }

            return await _userRepository.TryChargeResetPasswordCodeAttemptAsync(user.Id, cancellationToken);
        }

        private async Task<bool> ValidateUserTokenAsync(Command command, CancellationToken cancellationToken)
        {
            // lookup is (email, HASH of token). The reset token is stored hashed,
            // so hash the supplied raw code and compare — no plaintext comparison remains.
            var user = await _userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);

            return user is not null &&
                   user.ResetPasswordCode is not null &&
                   user.ResetPasswordCode == SecurityTokens.Hash(command.Code) &&
                   user.ResetPasswordCodeExpiresAt.HasValue &&
                   DateTime.UtcNow < user.ResetPasswordCodeExpiresAt.Value;
        }

        private async Task<bool> CheckIfPasswordDifferentAsync(Command command, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
            return user is not null && !command.NewPassword.CheckIfPasswordSame(user.Password!);
        }
    }

    public record Command(
        string Email,
        string NewPassword,
        string Code)
        : ICommand<Response>;

    public record Response(string Id);

    internal class Handler(
        IUserRepository userRepository,
        IRefreshTokenService refreshTokenService)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
            user!.UpdatePassword(command.NewPassword);
            user.ClearResetPasswordToken();

            // Reset completion is the account-takeover recovery path: the caller proves control
            // via the emailed code, not a live session, so EVERY refresh token dies — including
            // whatever sessions an attacker minted with the old credential (ADR-0024 D4.6;
            // keep-none, unlike the authenticated change which spares the caller's session).
            await refreshTokenService.RevokeAllForUserAsync(
                user.Id, "password_reset", exceptRawToken: null, cancellationToken);

            return BusinessResult.Success(new Response(Id: user.Id));
        }
    }
}