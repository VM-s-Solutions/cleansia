using Cleansia.Core.AppServices.Common;
using FluentValidation;
using System.Linq.Expressions;

namespace Cleansia.Core.AppServices.Features.Auth.Validators;

public class BaseAuthValidator<TRequest> : AbstractValidator<TRequest>
{
    protected void AddEmailRules(Expression<Func<TRequest, string>> emailExpression)
    {
        var propertyName = GetPropertyName(emailExpression);

        RuleFor(emailExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(propertyName)
            .EmailAddress()
            .WithMessage(BusinessErrorMessage.InvalidEmailFormat)
            .WithErrorCode(propertyName)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength)
            .WithErrorCode(propertyName);
    }

    protected void AddFirstNameRules(Expression<Func<TRequest, string>> firstNameExpression)
    {
        var propertyName = GetPropertyName(firstNameExpression);

        RuleFor(firstNameExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(propertyName)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength)
            .WithErrorCode(propertyName);
    }

    protected void AddLastNameRules(Expression<Func<TRequest, string>> lastNameExpression)
    {
        var propertyName = GetPropertyName(lastNameExpression);

        RuleFor(lastNameExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(propertyName)
            .MaximumLength(50)
            .WithMessage(BusinessErrorMessage.MaxLength)
            .WithErrorCode(propertyName);
    }

    protected void AddPasswordRules(Expression<Func<TRequest, string>> passwordExpression)
    {
        var propertyName = GetPropertyName(passwordExpression);
        // Requires: minimum 12 characters, at least one uppercase, one lowercase, one digit, one special character
        const string passwordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&#^()]).{12,}$";

        RuleFor(passwordExpression)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(BusinessErrorMessage.Required)
            .WithErrorCode(propertyName)
            .Matches(passwordPattern)
            .WithMessage(BusinessErrorMessage.InvalidPasswordFormat)
            .WithErrorCode(propertyName);
    }

    private static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty>> expression)
    {
        if (expression.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        throw new ArgumentException("Invalid property expression", nameof(expression));
    }
}
