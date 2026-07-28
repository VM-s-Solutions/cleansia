using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using FluentValidation;
using System.Linq.Expressions;

namespace Cleansia.Core.AppServices.Features.Auth.Validators;

/// <summary>
/// The single shared login validator: the email-existence/auth-type rules, the lockout-then-
/// password gate (Cascade.Stop so a locked account never evaluates the password), and the failed-login
/// counting are defined once here. Each host's login command — Login, PartnerLogin, AdminLogin — derives
/// from this with its own field selectors; the per-host profile gate lives in the handler, not here.
/// All user reads go through the IgnoringTenant variants: login is anonymous (no tenant claim), so the
/// global tenant filter would otherwise hide every tenant-stamped account.
/// </summary>
public abstract class LoginValidator<TCommand> : BaseAuthValidator<TCommand>
{
    // The auth-type rule cannot pick its message up front: the provider is only known after the async
    // user read inside the predicate. So the predicate hands the resolved message key to the rule
    // through the MessageFormatter, and the rule's template is nothing but this placeholder.
    private const string AuthTypeErrorPlaceholder = "AuthTypeError";
    private const string AuthTypeErrorTemplate = "{" + AuthTypeErrorPlaceholder + "}";

    private readonly IUserRepository userRepository;
    private readonly IRefreshTokenRepository refreshTokenRepository;
    private readonly IRefreshTokenService refreshTokenService;

    protected LoginValidator(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenService refreshTokenService,
        Expression<Func<TCommand, string>> emailSelector,
        Expression<Func<TCommand, string>> passwordSelector,
        Expression<Func<TCommand, bool>> rememberMeSelector,
        Expression<Func<TCommand, string?>> trustedDeviceTokenSelector)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        this.refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        this.refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));

        var emailName = PropertyName(emailSelector);
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
            .WhenAsync((command, cancellationToken) => UserAuthenticationTypeIsInternal(emailFunc(command), cancellationToken));

        RuleFor(emailSelector)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MustAsync(userRepository.ExistsWithEmailIgnoringTenantAsync)
            .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail)
            .WithErrorCode(emailName)
            .MustAsync((command, _, context, cancellationToken) => UserAuthenticationTypeIsInternal(emailFunc(command), context, cancellationToken))
            .WithMessage(AuthTypeErrorTemplate)
            .WithErrorCode(emailName);

        RuleFor(rememberMeSelector)
            .NotNull()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(rememberMeName);
    }

    private async Task<bool> UserAuthenticationTypeIsInternal(string email, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailIgnoringTenantAsync(email, cancellationToken);
        return user is not null && user.AuthenticationType == AuthenticationType.Internal;
    }

    // Same gate as above — only an Internal account may sign in with a password — but on rejection it
    // also records WHICH provider the account actually uses, so the caller is not told "you signed in
    // with Google" when the account is an Apple account (the production bug this replaces: the message
    // was hardcoded to Google). No new disclosure: the preceding existence rule already stops the
    // cascade for an unknown email, so naming the provider only refines an error the caller already
    // gets for an address that is known to exist.
    private async Task<bool> UserAuthenticationTypeIsInternal(string email, ValidationContext<TCommand> context, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailIgnoringTenantAsync(email, cancellationToken);
        if (user is not null && user.AuthenticationType == AuthenticationType.Internal)
        {
            return true;
        }

        // user is null only if the account disappeared between the existence rule and this one; reuse
        // that rule's message rather than inventing a provider for an account that is not there.
        context.MessageFormatter.AppendArgument(
            AuthTypeErrorPlaceholder,
            user is null ? BusinessErrorMessage.NotExistingUserWithEmail : AuthTypeErrorMessages.For(user.AuthenticationType));

        return false;
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
        var user = await userRepository.GetByEmailIgnoringTenantAsync(email, cancellationToken);
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
        var userEntity = await userRepository.GetByEmailIgnoringTenantAsync(email, cancellationToken);
        if (userEntity is null)
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

    private static string PropertyName(LambdaExpression expression)
    {
        return expression.Body is MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("Invalid property expression", nameof(expression));
    }
}
