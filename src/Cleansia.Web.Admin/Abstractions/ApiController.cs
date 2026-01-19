using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Abstractions;

[ApiController]
public abstract class ApiController(IMediator mediator) : ControllerBase
{
    protected readonly IMediator Mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    protected IActionResult HandleResult<T>(BusinessResult result)
    {
        return result switch
        {
            { IsSuccess: false } => HandleFailure<T>(result),
            { IsSuccess: true } => HandleSuccess<T>(result),
            _ => throw new InvalidOperationException()
        };
    }

    protected IActionResult HandleFailure<T>(BusinessResult result)
    {
        return result switch
        {
            IValidationResult validationResult => BadRequest(CreateProblemDetails(
                        "Validation Error",
                        StatusCodes.Status400BadRequest,
                        result.Error!,
                        validationResult.Errors)),
            BusinessResult<JwtTokenResponse> authResult => Unauthorized(authResult.Error),
            _ => BadRequest(CreateProblemDetails(
                        "Bad Request",
                        StatusCodes.Status400BadRequest,
                        result.Error!))
        };
    }

    protected IActionResult HandleSuccess<T>(BusinessResult result)
    {
        return result switch
        {
            BusinessResult<T> successResult => Ok(successResult.Value),
            _ => Ok()
        };
    }

    private static ProblemDetails CreateProblemDetails(string title, int status, Error error, Error[]? errors = null)
    {
        var errorDetails = errors?.ToDictionary(
                error => error.Code,
                error => error.Message
            ) ?? [];

        return new ProblemDetails
        {
            Title = title,
            Type = error.Code,
            Detail = error.Message,
            Status = status,
            Extensions = { { nameof(errors), errorDetails } }
        };
    }
}
