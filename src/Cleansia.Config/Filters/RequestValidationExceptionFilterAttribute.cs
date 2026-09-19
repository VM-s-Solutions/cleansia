using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Tenancy;
using Cleansia.Infra.Common.Validations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Cleansia.Config.Filters;

/// <summary>
/// The throwing arm of a validation reject. A request whose response type cannot carry a
/// <c>ValidationResult</c> (a paged read) surfaces its rule failures as a
/// <see cref="RequestValidationException"/>; this answers it with the ProblemDetails the returning
/// arm builds, so the wire shape is one whichever way the reject travelled. A write against a company
/// frozen for archive is refused at the commit and answered here as 409 with the same body shape and
/// one keyed error (ADR-0064 D3). Anything else falls through to the host's exception handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequestValidationExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case RequestValidationException exception:
                context.Result = new BadRequestObjectResult(CleansiaApiController.CreateValidationProblemDetails(exception.Errors));
                context.ExceptionHandled = true;
                break;
            case CompanyArchivedException:
                context.Result = new ObjectResult(CleansiaApiController.CreateArchivedCompanyProblemDetails(
                    new Error(nameof(ITenantEntity.TenantId), BusinessErrorMessage.TenantArchived)))
                {
                    StatusCode = StatusCodes.Status409Conflict,
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}
