using Cleansia.Infra.Common.Validations;

namespace Cleansia.Core.AppServices.Behaviors;

/// <summary>
/// A validation reject on a request whose response type cannot carry one — a paged read returning
/// <c>PagedData&lt;T&gt;</c>, a list. <c>ValidationPipelineBehavior</c> throws it where it would
/// otherwise return a <c>ValidationResult</c>; the host answers it with the same 400 ProblemDetails
/// the returning arm produces, so a client sees one shape whichever way the reject travelled.
/// </summary>
public sealed class RequestValidationException(Error[] errors)
    : Exception(IValidationResult.ValidationError.Message)
{
    public Error[] Errors { get; } = errors;
}
