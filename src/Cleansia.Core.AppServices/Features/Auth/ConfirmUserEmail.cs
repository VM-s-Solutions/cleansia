using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Proves the typed code against the named account and opens its first session. The partner hosts route
/// it for their cleaners, and a host that is not a customer host confirms an Employee or an Administrator
/// only — whom it signs in is <see cref="PartnerLogin"/>'s rule, and a Customer's code presented there
/// would otherwise mint a partner-audience session for an account that has no business on that host.
/// </summary>
[AuditAction("customer.account.email_confirmed", Audience = AuditAudience.Customer, ResourceType = "User", AllowsAnonymousActor = true)]
public class ConfirmUserEmail
{
    // Two disjoint wire shapes (see SecurityTokens): the typed OTP is exactly 6 digits; the legacy
    // self-authenticating tokens are 22-char base64url. Length discriminates the branch.
    private static bool IsOtp(string? code) => code?.Length == SecurityTokens.OtpLength;

    public class Validator : AbstractValidator<Command>
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<Validator> _logger;
        private readonly IAuditContext _auditContext;

        public Validator(IUserRepository userRepository, ILogger<Validator> logger, IAuditContext auditContext)
        {
            _userRepository = userRepository;
            _logger = logger;
            _auditContext = auditContext;

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

        // Whichever rule refuses, the account it refused is already named on the audit context.
        private async Task<User?> ResolveAsync(Command command, CancellationToken cancellationToken)
        {
            var user = await Resolve(_userRepository, command, cancellationToken);
            if (user is not null)
            {
                _auditContext.RecordEvidence("User", user.Id, payload: null, actorUserId: user.Id);
            }

            return user;
        }
    }

    /// <param name="Code">The 6-digit typed verification code (or a legacy 22-char link token still
    /// in flight from before the OTP switch).</param>
    /// <param name="Email">The account the code was issued to. REQUIRED with a 6-digit code (the code
    /// only proves possession relative to a named account); ignored on the legacy-token branch, which
    /// keeps the old code-only wire shape so existing clients and in-flight emails stay valid.</param>
    public record Command(string Code, string? Email = null) : ICommand<JwtTokenResponse>, IOperatorScopedRequest
    {
        // The confirmation names no market: a refusal that resolved no account is stamped with the default
        // market's operator (ADR-0061 D3), and one on a known account is re-stamped by the failure sink
        // with that account's operator. Off the wire.
        string? IOperatorScopedRequest.CountryId => null;
    }

    /// <summary>Which wire shape confirmed the address: the typed code, or a link minted before the OTP switch.</summary>
    public record EmailConfirmationEvidence(string Method) : ICustomerAuditPayload
    {
        public const string OtpMethod = "Otp";
        public const string LegacyLinkMethod = "LegacyLink";
    }

    public class Handler(
        ITokenService tokenService,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience,
        IAuditContext auditContext) : ICommandHandler<Command, JwtTokenResponse>
    {
        private bool IsCustomerHost => hostAudience.Audience == JwtAudiences.Customer;

        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            // Same resolution the validator proved — a diverging load here would NRE into a 500.
            var user = await Resolve(userRepository, command, cancellationToken);

            // Behind the proven code on purpose, not a validator rule ahead of it: refused before the code
            // is checked, the key would tell an anonymous caller which addresses hold a Customer account.
            if (!IsCustomerHost && user!.Profile is not (UserProfile.Employee or UserProfile.Administrator))
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Email), BusinessErrorMessage.InsufficientPrivileges));
            }

            user!.ConfirmEmail();

            auditContext.RecordEvidence(
                "User",
                user.Id,
                new EmailConfirmationEvidence(IsOtp(command.Code) ? EmailConfirmationEvidence.OtpMethod : EmailConfirmationEvidence.LegacyLinkMethod),
                actorUserId: user.Id);

            return BusinessResult.Success(await tokenService.GenerateTokenAsync(user, rememberMe: true, hostAudience.Audience, cancellationToken));
        }
    }

    // The single account-resolution seam BOTH the validator and the handler use (a validator/handler
    // query divergence is an NRE factory — see the RefreshToken post-mortem).
    //   - OTP: by email, anonymous path → tenant-ignoring (same posture as the ChangePassword reset
    //     flow; email is one identity across the holding, ADR-0061 D5.1).
    //   - Legacy 128-bit token: by code hash alone, tenant-ignoring — the link is clicked anonymously
    //     while the row it confirms is stamped (ADR-0061 D4), and the pin is the server-issued hash,
    //     which 128 bits cannot be guessed into someone else's account. Kept so in-flight pre-OTP
    //     emails still confirm.
    private static Task<User?> Resolve(IUserRepository userRepository, Command command, CancellationToken cancellationToken)
    {
        if (!IsOtp(command.Code))
        {
            return userRepository.GetByConfirmationCodeIgnoringTenantAsync(command.Code, cancellationToken);
        }

        return string.IsNullOrEmpty(command.Email)
            ? Task.FromResult<User?>(null)
            : userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
    }
}
