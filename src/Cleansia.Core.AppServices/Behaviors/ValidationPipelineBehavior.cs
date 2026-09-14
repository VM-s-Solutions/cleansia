using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cleansia.Core.AppServices.Behaviors;

/// <summary>
/// Runs every registered validator for every request type. The behavior used to be constrained to
/// <c>TResponse : BusinessResult</c>, which made the container skip it for anything else — a paged
/// read's <c>IRequest&lt;PagedData&lt;T&gt;&gt;</c> validator was registered, discoverable, and never
/// once executed. A reject now takes one of two arms by what the response can carry: a
/// <see cref="BusinessResult"/> comes back as the typed <see cref="ValidationResult"/>; any other
/// response type throws <see cref="RequestValidationException"/> with the same keyed errors, and the
/// host maps it to the same 400.
/// </summary>
public class ValidationPipelineBehavior<TRequest, TResponse>
    (IEnumerable<IValidator<TRequest>> validators,
     ILogger<ValidationPipelineBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly bool ResponseCarriesFailure = typeof(BusinessResult).IsAssignableFrom(typeof(TResponse));

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
                    // Keyed on the FIELD that broke — which is not what ErrorCode holds by default.
                    //
                    // FluentValidation always populates ErrorCode, defaulting it to the type name of
                    // the rule that fired. So every failure with no explicit code arrived as
                    // "NotEmptyValidator" / "MaximumLengthValidator", naming the rule rather than
                    // the field. A client could not highlight an input, and CreateProblemDetails
                    // groups by this key, so two empty fields collapsed into ONE entry with their
                    // messages "; "-joined. A customer submitting an order with no phone number was
                    // told `{ NotEmptyValidator: "common.required" }` and could not act on it.
                    //
                    // Fifty-eight rules DO set a code deliberately, and most name a property anyway
                    // (`WithErrorCode(nameof(Command.FirstName))`) — but some deliberately do not
                    // (`nameof(BlobFileDto)` for a file's four rules, "Language"), and overriding
                    // those with the property name would discard the grouping they were asking for.
                    // The default is distinguishable: it is a validator type name, and it always
                    // ends in "Validator". Nothing here names a property that way.
                    //
                    // The VALUE is untouched — still the BusinessErrorMessage constant every client
                    // localizes from. PropertyName is empty for a whole-command rule
                    // (`RuleFor(x => x)`), which keeps the code it has.
                    .Select(failure => new Error(NameOf(failure), failure.ErrorMessage));
            })))
            .SelectMany(validationFailures => validationFailures)
            .Distinct()
            .ToArray();

        if (errors.Length == 0)
        {
            return await next(cancellationToken);
        }

        if (ResponseCarriesFailure)
        {
            return (TResponse)(object)CreateValidationResult(typeof(TResponse), errors);
        }

        throw new RequestValidationException(errors);
    }

    /// <summary>
    /// The key a client reads to find the offending field: an explicitly-set error code where there
    /// is one, otherwise the property, otherwise FluentValidation's default.
    /// </summary>
    private static string NameOf(FluentValidation.Results.ValidationFailure failure)
    {
        var code = failure.ErrorCode;
        var isFluentValidationDefault = string.IsNullOrEmpty(code) || code.EndsWith("Validator", StringComparison.Ordinal);

        if (!isFluentValidationDefault)
        {
            return code;
        }

        return string.IsNullOrEmpty(failure.PropertyName) ? code : failure.PropertyName;
    }

    internal static TResult CreateValidationResult<TResult>(Error[] errors)
        where TResult : BusinessResult =>
        (TResult)CreateValidationResult(typeof(TResult), errors);

    private static BusinessResult CreateValidationResult(Type resultType, Error[] errors)
    {
        if (resultType == typeof(BusinessResult))
        {
            return ValidationResult.WithErrors(errors);
        }

        var validationResult = typeof(ValidationResult<>)
            .GetGenericTypeDefinition()
            .MakeGenericType(resultType.GenericTypeArguments[0])
            .GetMethod(nameof(ValidationResult.WithErrors))!
            .Invoke(null, [errors])!;

        return (BusinessResult)validationResult;
    }
}
