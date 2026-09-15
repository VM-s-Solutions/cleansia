using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Behaviors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cleansia.Config.Filters;

/// <summary>
/// The throwing arm of a validation reject. A request whose response type cannot carry a
/// <c>ValidationResult</c> (a paged read) surfaces its rule failures as a
/// <see cref="RequestValidationException"/>; this answers it with the ProblemDetails the returning
/// arm builds, so the wire shape is one whichever way the reject travelled. Anything else falls
/// through to the host's exception handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequestValidationExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not RequestValidationException exception)
        {
            return;
        }

        context.Result = new BadRequestObjectResult(CleansiaApiController.CreateValidationProblemDetails(exception.Errors));
        context.ExceptionHandled = true;
    }
}
