using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(IMediator mediator) : ApiController(mediator)
{
    [AllowAnonymous]
    [HttpPost("Register")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] Register.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<bool>(result);
    }

    [AllowAnonymous]
    [HttpPost("RegisterEmployee")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterEmployee([FromBody] RegisterEmployee.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<bool>(result);
    }

    [AllowAnonymous]
    [HttpPost("Login")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] Login.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<JwtTokenResponse>(result);
    }

    [AllowAnonymous]
    [HttpPost("GoogleAuth")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GoogleAuth([FromBody] GoogleAuth.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<JwtTokenResponse>(result);
    }

    [AllowAnonymous]
    [HttpPut("ConfirmUserEmail")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmUserEmail([FromBody] ConfirmUserEmail.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<JwtTokenResponse>(result);
    }

    [AllowAnonymous]
    [HttpPost("ResendConfirmationEmail")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmail.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);

        return HandleResult<bool>(result);
    }
}