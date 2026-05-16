using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Behaviors;

public class ValidationPipelineBehavior<TRequest, TResponse>
    (IEnumerable<IValidator<TRequest>> validators,
     ILogger<ValidationPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : BusinessResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            var requestType = typeof(TRequest);
            if (requestType.Name == "Command" || requestType.DeclaringType?.Name.EndsWith("Command") == true)
            {
#if DEBUG
                throw new InvalidOperationException(
                    $"Command {requestType.FullName} has no FluentValidation validator. " +
                    "Add one (even an empty AbstractValidator<Command>) or rename to Query.");
#else
                logger.LogCritical(
                    "Command {RequestType} has no FluentValidation validator — pipeline failed open. " +
                    "Add a validator immediately.", requestType.FullName);
#endif
            }
            return await next(cancellationToken);
        }

        var errors = (await Task.WhenAll(validators
            .Select(async validator =>
            {
                var validationResult = await validator.ValidateAsync(request, cancellationToken);
                return validationResult.Errors
                    .Where(validationFailure => validationFailure is not null)
                    .Select(failure => new Error(failure.ErrorCode, failure.ErrorMessage));
            })))
            .SelectMany(validationFailures => validationFailures)
            .Distinct()
            .ToArray();

        if (errors.Length != 0)
        {
            return CreateValidationResult<TResponse>(errors);
        }

        return await next(cancellationToken);
    }

    private static TResult CreateValidationResult<TResult>(Error[] errors)
        where TResult : BusinessResult
    {
        if (typeof(TResult) == typeof(BusinessResult))
        {
            return (ValidationResult.WithErrors(errors) as TResult)!;
        }

        var validationResult = typeof(ValidationResult<>)
            .GetGenericTypeDefinition()
            .MakeGenericType(typeof(TResult).GenericTypeArguments[0])
            .GetMethod(nameof(ValidationResult.WithErrors))!
            .Invoke(null,[errors])!;

        return (validationResult as TResult)!;
    }
}
