using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminAuthController(IMediator mediator) : ApiController(mediator)
{
    /// <summary>
    /// Admin-specific login endpoint that validates user has Administrator or Employee role.
    /// Returns 400 Bad Request with InsufficientPrivileges error if user lacks required role.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("Login")]
    [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AdminLogin.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<JwtTokenResponse>(result);
    }
}
