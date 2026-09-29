using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using FluentValidation;
using System.Linq.Expressions;

namespace Cleansia.Core.AppServices.Common.Validators.Auth;

/// <summary>
/// The single shared login validator: the lockout-then-credentials gate (Cascade.Stop so a locked
/// account never evaluates the password) and the failed-login counting are defined once here. Each
/// host's login command — Login, PartnerLogin, AdminLogin — derives from this with its own field
/// selectors; the per-host profile gate lives in the handler, not here. An unknown address, an account
/// that signs in with Google or Apple and a wrong password all get the same refusal, so the sign-in
/// never tells a caller whether an address is registered or how it signs in.
/// All user reads go through the IgnoringTenant variants: login is anonymous (no tenant claim), so the
/// global tenant filter would otherwise hide every tenant-stamped account.
/// </summary>
public abstract class LoginValidator<TCommand> : BaseAuthValidator<TCommand>
{
    private readonly IUserRepository userRepository;
    private readonly IRefreshTokenRepository refreshTokenRepository;
    private readonly IRefreshTokenService refreshTokenService;
    private readonly IAuditContext auditContext;

    protected LoginValidator(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        IAuditContext auditContext,
        Expression<Func<TCommand, string>> emailSelector,
        Expression<Func<TCommand, string>> passwordSelector,
        Expression<Func<TCommand, bool>> rememberMeSelector,
        Expression<Func<TCommand, string?>> trustedDeviceTokenSelector)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        this.refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
        this.auditContext = auditContext ?? throw new ArgumentNullException(nameof(auditContext));

        var passwordName = PropertyName(passwordSelector);
        var rememberMeName = PropertyName(rememberMeSelector);

        var emailFunc = emailSelector.Compile();
        var passwordFunc = passwordSelector.Compile();
        var trustedDeviceTokenFunc = trustedDeviceTokenSelector.Compile();

        AddEmailRules(emailSelector);

        RuleFor(passwordSelector)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MustAsync((command, _, cancellationToken) => AccountIsNotLockedOutOrTrustedDevice(emailFunc(command), trustedDeviceTokenFunc(command), cancellationToken))
            .WithMessage(BusinessErrorMessage.AccountLocked)
            .WithErrorCode(passwordName)
            .MustAsync((command, _, cancellationToken) => HasValidPassword(emailFunc(command), passwordFunc(command), cancellationToken))
            .WithMessage(BusinessErrorMessage.InvalidPassword)
            .WithErrorCode(passwordName)
            .When(command => !string.IsNullOrWhiteSpace(emailFunc(command)));

        RuleFor(rememberMeSelector)
            .NotNull()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(rememberMeName);
    }

    // The lockout check precedes the password check (Cascade.Stop), so a locked account never
    // evaluates the password — no correctness oracle and no further counting while locked. A device
    // that still presents a valid, non-revoked, non-expired refresh token bound to THIS account
    // (the trusted-device marker, read server-side) bypasses the lock so the password rule below can
    // run — restoring access for the legit user without granting a session (S1-S4: the marker only
    // gates the password check). An absent/forged/revoked/expired/wrong-account token leaves the lock
    // standing, identical to the baseline lockout behavior, so a credential-sprayer gains no new oracle.
    private async Task<bool> AccountIsNotLockedOutOrTrustedDevice(string email, string? trustedDeviceToken, CancellationToken cancellationToken)
    {
        var user = await ResolveAsync(email, cancellationToken);
        if (user is null || !user.IsLockedOut(DateTimeOffset.UtcNow))
        {
            return true;
        }

        return await IsTrustedDeviceForAccount(user.Id, trustedDeviceToken, cancellationToken);
    }

    private async Task<bool> IsTrustedDeviceForAccount(string accountId, string? trustedDeviceToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(trustedDeviceToken))
        {
            return false;
        }

        var hash = refreshTokenService.HashToken(trustedDeviceToken);
        var token = await refreshTokenRepository.GetByTokenHashAsync(hash, cancellationToken);

        return token is not null && token.IsAlive && token.UserId == accountId;
    }

    private async Task<bool> HasValidPassword(string email, string password, CancellationToken cancellationToken)
    {
        var userEntity = await ResolveAsync(email, cancellationToken);
        if (userEntity is null || userEntity.AuthenticationType != AuthenticationType.Internal)
        {
            return false;
        }

        if (password.CheckIfPasswordSame(userEntity.Password!))
        {
            return true;
        }

        await userRepository.RecordFailedLoginAsync(email, DateTimeOffset.UtcNow, cancellationToken);
        return false;
    }

    // Whichever rule refuses, the account it refused is already named on the audit context: a refusal on
    // a known account is that account's row, and only the validator resolves the address before it.
    private async Task<User?> ResolveAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailIgnoringTenantAsync(email, cancellationToken);
        if (user is not null)
        {
            auditContext.RecordEvidence("User", user.Id, payload: null, actorUserId: user.Id, actorProfile: user.Profile);
        }

        return user;
    }

    private static string PropertyName(LambdaExpression expression)
    {
        return expression.Body is MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("Invalid property expression", nameof(expression));
    }
}
