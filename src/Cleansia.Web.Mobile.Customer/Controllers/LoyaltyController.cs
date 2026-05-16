using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Loyalty;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class LoyaltyController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpGet("GetMy")]
    [Permission(Policy.CanViewMyLoyalty)]
    [ProducesResponseType(typeof(GetMyLoyalty.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMy(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyLoyalty.Query(), cancellationToken);
        return HandleResult<GetMyLoyalty.Response>(result);
    }

    [HttpGet("GetActivity")]
    [Permission(Policy.CanViewMyLoyalty)]
    [ProducesResponseType(typeof(PagedData<GetLoyaltyActivity.ActivityItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<GetLoyaltyActivity.ActivityItem>> GetActivity(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        return await Mediator.Send(new GetLoyaltyActivity.Query(offset, limit), cancellationToken);
    }

    [HttpGet("GetTiers")]
    [Permission(Policy.CanViewMyLoyalty)]
    [ProducesResponseType(typeof(GetLoyaltyTiers.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTiers(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetLoyaltyTiers.Query(), cancellationToken);
        return HandleResult<GetLoyaltyTiers.Response>(result);
    }
}
