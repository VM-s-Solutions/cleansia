using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
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
/// </summary>
public abstract class LoginValidator<TCommand> : BaseAuthValidator<TCommand>
{
    private readonly IUserRepository userRepository;

    protected LoginValidator(
        IUserRepository userRepository,
        Expression<Func<TCommand, string>> emailSelector,
        Expression<Func<TCommand, string>> passwordSelector,
        Expression<Func<TCommand, bool>> rememberMeSelector)
    {
        this.userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

        var emailName = PropertyName(emailSelector);
        var passwordName = PropertyName(passwordSelector);
        var rememberMeName = PropertyName(rememberMeSelector);

        var emailFunc = emailSelector.Compile();
        var passwordFunc = passwordSelector.Compile();

        AddEmailRules(emailSelector);

        RuleFor(passwordSelector)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MustAsync((command, _, cancellationToken) => AccountIsNotLockedOut(emailFunc(command), cancellationToken))
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
            .MustAsync(userRepository.ExistsWithEmailAsync)
            .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail)
            .WithErrorCode(emailName)
            .MustAsync(UserAuthenticationTypeIsInternal)
            .WithMessage(BusinessErrorMessage.GoogleAuthTypeError)
            .WithErrorCode(emailName);

        RuleFor(rememberMeSelector)
            .NotNull()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(rememberMeName);
    }

    private async Task<bool> UserAuthenticationTypeIsInternal(string email, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        return user is not null && user.AuthenticationType == AuthenticationType.Internal;
    }

    // The lockout check precedes the password check (Cascade.Stop), so a locked account never
    // evaluates the password — no correctness oracle and no further counting while locked.
    private async Task<bool> AccountIsNotLockedOut(string email, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(email, cancellationToken);
        return user is null || !user.IsLockedOut(DateTimeOffset.UtcNow);
    }

    private async Task<bool> HasValidPassword(string email, string password, CancellationToken cancellationToken)
    {
        var userEntity = await userRepository.GetByEmailAsync(email, cancellationToken);
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
