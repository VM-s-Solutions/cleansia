using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class GdprController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpGet("export")]
    [Permission(Policy.CanExportOwnData)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(GdprExportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMyData(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ExportUserData.Query(), cancellationToken);
        return HandleResult<GdprExportDto>(result);
    }

    [HttpPost("delete-account")]
    [Permission(Policy.CanDeleteOwnAccount)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMyAccount(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteUserAccount.Command(), cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpGet("consents")]
    [Permission(Policy.CanViewOwnConsents)]
    [ProducesResponseType(typeof(List<UserConsentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyConsents(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetUserConsents.Query(), cancellationToken);
        return HandleResult<List<UserConsentDto>>(result);
    }

    [HttpPost("consents")]
    [Permission(Policy.CanGrantConsent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GrantConsent([FromBody] GrantConsent.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpPost("consents/withdraw")]
    [Permission(Policy.CanWithdrawConsent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> WithdrawConsent([FromBody] WithdrawConsent.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<object>(result);
    }
}
