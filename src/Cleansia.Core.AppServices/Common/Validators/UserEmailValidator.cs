using Cleansia.Core.Domain.Repositories;
using FluentValidation;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Cleansia.Core.AppServices.Common.Validators;

public class UserEmailValidator<TRequest> : AbstractValidator<TRequest>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSessionProvider _userSessionProvider;

    public UserEmailValidator(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider)
    {
        _userSessionProvider = userSessionProvider ?? throw new ArgumentNullException(nameof(userSessionProvider));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));

        RuleFor(request => request)
            .MustAsync(UserWithEmailExistsAsync)
            .WithMessage(BusinessErrorMessage.NotExistingUserWithEmail);
    }

    protected async Task<bool> UserWithEmailExistsAsync(TRequest request, CancellationToken cancellationToken)
    {
        var userEmailClaim = _userSessionProvider.GetUserEmail();
        if (userEmailClaim is null)
        {
            return false;
        }
        var user = await _userRepository.GetByEmailAsync(userEmailClaim, cancellationToken);
        return user?.IsEmailConfirmed ?? false;
    }
}

public abstract class BaseUserValidator<TRequest> : AbstractValidator<TRequest>
{
    protected void AddEmailRules(Expression<Func<TRequest, string>> emailExpression)
    {
        RuleFor(emailExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .EmailAddress()
            .WithMessage(BusinessErrorMessage.InvalidEmailFormat)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength);
    }

    protected void AddFirstNameRules(Expression<Func<TRequest, string>> firstNameExpression)
    {
        RuleFor(firstNameExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength);
    }

    protected void AddLastNameRules(Expression<Func<TRequest, string>> lastNameExpression)
    {
        RuleFor(lastNameExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength);
    }

    protected void AddPasswordRules(Expression<Func<TRequest, string>> passwordExpression)
    {
        RuleFor(passwordExpression).ValidatePassword();
    }
}
