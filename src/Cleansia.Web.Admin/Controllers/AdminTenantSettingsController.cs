using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

/// <summary>
/// The admin's own operating company's settings — every catalogue key, set or reset one at a time.
/// The company is the caller's tenant claim; there is no way to name another.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class AdminTenantSettingsController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-all")]
    [Permission(Policy.CanViewTenantConfigurations)]
    [ProducesResponseType(typeof(GetTenantSettings.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetTenantSettings.Query(), cancellationToken);
        return HandleResult<GetTenantSettings.Response>(result);
    }

    [HttpPut("set")]
    [Permission(Policy.CanUpdateTenantConfiguration)]
    [ProducesResponseType(typeof(SetTenantSetting.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Set(
        [FromBody] SetTenantSetting.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SetTenantSetting.Response>(result);
    }

    [HttpDelete("reset/{key}")]
    [Permission(Policy.CanDeleteTenantConfiguration)]
    [ProducesResponseType(typeof(ResetTenantSetting.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Reset(string key, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ResetTenantSetting.Command(key), cancellationToken);
        return HandleResult<ResetTenantSetting.Response>(result);
    }
}
