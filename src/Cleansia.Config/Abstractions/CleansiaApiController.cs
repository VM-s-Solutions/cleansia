using Asp.Versioning;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Config.Abstractions;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class CleansiaApiController(IMediator mediator) : ControllerBase
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

    protected IActionResult HandleRedirectResult(BusinessResult result, string url)
    {
        return result switch
        {
            { IsSuccess: false } => HandleRedirectFailure(result, url),
            { IsSuccess: true } => HandleRedirectSuccess(result, url),
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

    protected IActionResult HandleRedirectFailure(BusinessResult result, string url)
    {
        return result switch
        {
            IValidationResult validationResult => Redirect(CreateRedirectUrl(url, result.IsSuccess, [result.Error!, .. validationResult.Errors])),
            _ => Redirect(CreateRedirectUrl(url, result.IsSuccess, [result.Error!]))
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

    protected IActionResult HandleRedirectSuccess(BusinessResult result, string url)
    {
        return Redirect(CreateRedirectUrl(url, result.IsSuccess));
    }

    private static ProblemDetails CreateProblemDetails(string title, int status, Error error, Error[]? errors = null)
    {
        var errorDetails = errors?
                .Where(e => e.Code is not null)
                .GroupBy(e => e.Code!)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join("; ", g.Select(e => e.Message))
                ) ?? new Dictionary<string, string>();

        return new ProblemDetails
        {
            Title = title,
            Type = error.Code,
            Detail = error.Message,
            Status = status,
            Extensions = { { nameof(errors), errorDetails } }
        };
    }

    private static string CreateRedirectUrl(string url, bool isSuccess, Error[]? errors = null)
    {
        var query = new List<string> { $"&isSuccess={isSuccess}" };

        if (isSuccess || errors == null)
        {
            return $"{url}?{string.Join('&', query)}";
        }

        var errorMessages = string.Join(',', errors.Select(error => error.Message));
        query.Add($"errors={errorMessages}");

        return $"{url}?{string.Join('&', query)}";
    }
}
