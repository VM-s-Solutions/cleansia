using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Auth;

public class ConfirmUserEmail
{
    // Two disjoint wire shapes (see SecurityTokens): the typed OTP is exactly 6 digits; the legacy
    // self-authenticating tokens are 22-char base64url. Length discriminates the branch.
    private static bool IsOtp(string? code) => code?.Length == SecurityTokens.OtpLength;

    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<Validator> _logger;

        public Validator(IUserRepository userRepository, ILogger<Validator> logger)
        {
            _userRepository = userRepository;
            _logger = logger;

            // A 6-digit OTP is guessable in isolation, so it is NEVER resolved by the bare code — the
            // email names the single account whose stored hash the code is compared against.
            RuleFor(command => command.Email)
                .NotEmpty()
                .When(command => IsOtp(command.Code))
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Email));

            RuleFor(command => command.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .WithErrorCode(nameof(Command.Code))
                .MustAsync(HasAttemptBudgetAsync)
                .WithMessage(BusinessErrorMessage.TooManyAttempts)
                .WithErrorCode(nameof(Command.Code))
                .MustAsync(ValidateUserTokenAsync)
                .WithMessage(BusinessErrorMessage.InvalidConfirmationCode)
                .WithErrorCode(nameof(Command.Code));
        }

        // Every attempt that RESOLVES an account consumes one unit of that account's per-code budget
        // BEFORE validity is evaluated — once the budget is spent even the correct live code is
        // refused, so a guessing run cannot convert a late hit into a confirmation (ADR-0003
        // residual: per-code attempt cap). On the OTP branch the account is named by email, so every
        // guess against that account charges it (5 tries per issued code; a resend resets the budget).
        // Unresolvable attempts carry no account to charge; on the legacy branch they stay bounded by
        // the 128-bit token entropy plus the per-IP auth window.
        private async Task<bool> HasAttemptBudgetAsync(Command command, string code, CancellationToken cancellationToken)
        {
            var user = await ResolveAsync(command, cancellationToken);
            if (user is null)
            {
                return true;
            }

            var charged = await _userRepository.TryChargeConfirmationCodeAttemptAsync(user.Id, cancellationToken);
            if (!charged)
            {
                // S6: log the user id only — never the code.
                _logger.LogWarning("Email confirmation refused for user {UserId}: per-code attempt cap reached.", user.Id);
            }

            return charged;
        }

        private async Task<bool> ValidateUserTokenAsync(Command command, string code, CancellationToken cancellationToken)
        {
            var user = await ResolveAsync(command, cancellationToken);

            if (user is null)
            {
                // S6: never log the raw (or hashed) code; no user identity to log either.
                _logger.LogWarning("Email confirmation failed: no account matched the supplied code.");
                return false;
            }

            // OTP branch: resolution was by email, so the code itself is proven here — compare the
            // hash against the named account's stored column (never plaintext; mirrors ChangePassword).
            if (IsOtp(code)
                && (user.ConfirmationCode is null || user.ConfirmationCode != SecurityTokens.Hash(code)))
            {
                _logger.LogWarning("Email confirmation failed for user {UserId}: code mismatch.", user.Id);
                return false;
            }

            if (!user.ConfirmationCodeExpiresAt.HasValue || DateTime.UtcNow >= user.ConfirmationCodeExpiresAt.Value)
            {
                _logger.LogWarning("Email confirmation failed for user {UserId}: code expired.", user.Id);
                return false;
            }

            return true;
        }

        private Task<User?> ResolveAsync(Command command, CancellationToken cancellationToken)
            => Resolve(_userRepository, command, cancellationToken);
    }

    /// <param name="Code">The 6-digit typed verification code (or a legacy 22-char link token still
    /// in flight from before the OTP switch).</param>
    /// <param name="Email">The account the code was issued to. REQUIRED with a 6-digit code (the code
    /// only proves possession relative to a named account); ignored on the legacy-token branch, which
    /// keeps the old code-only wire shape so existing clients and in-flight emails stay valid.</param>
    public record Command(string Code, string? Email = null) : ICommand<JwtTokenResponse>;

    public class Handler(
        ITokenService tokenService,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience) : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Same resolution the validator proved — a diverging load here would NRE into a 500.
            var user = await Resolve(userRepository, command, cancellationToken);
            user!.ConfirmEmail();

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }

    // The single account-resolution seam BOTH the validator and the handler use (a validator/handler
    // query divergence is an NRE factory — see the RefreshToken post-mortem).
    //   - OTP: by email, anonymous path → tenant-ignoring (same posture as the ChangePassword reset
    //     flow; email uniqueness is per-tenant, and the hash compare disambiguates in practice).
    //   - Legacy 128-bit token: by code hash alone — safe only because 128 bits cannot be guessed
    //     into someone else's account; kept so in-flight pre-OTP emails still confirm.
    private static Task<User?> Resolve(IUserRepository userRepository, Command command, CancellationToken cancellationToken)
    {
        if (!IsOtp(command.Code))
        {
            return userRepository.GetByConfirmationCodeAsync(command.Code, cancellationToken);
        }

        return string.IsNullOrEmpty(command.Email)
            ? Task.FromResult<User?>(null)
            : userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
    }
}
