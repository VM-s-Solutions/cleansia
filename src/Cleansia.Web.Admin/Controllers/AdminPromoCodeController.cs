using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminPromoCodeController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewPromoCodes)]
    [ProducesResponseType(typeof(PagedData<PromoCodeListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedPromoCodes(
        [FromQuery] bool? active,
        [FromQuery] bool? expired,
        [FromQuery] string? searchCode,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetPagedPromoCodes.Query(active, expired, searchCode, offset, limit),
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{promoCodeId}")]
    [Permission(Policy.CanViewPromoCodes)]
    [ProducesResponseType(typeof(PromoCodeDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPromoCodeById(
        string promoCodeId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPromoCodeById.Query(promoCodeId), cancellationToken);
        return HandleResult<PromoCodeDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreatePromoCode)]
    [ProducesResponseType(typeof(CreatePromoCode.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePromoCode(
        [FromBody] CreatePromoCode.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreatePromoCode.Response>(result);
    }

    [HttpPut("update/{promoCodeId}")]
    [Permission(Policy.CanUpdatePromoCode)]
    [ProducesResponseType(typeof(UpdatePromoCode.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePromoCode(
        string promoCodeId,
        [FromBody] UpdatePromoCode.Command command,
        CancellationToken cancellationToken)
    {
        if (command.PromoCodeId != promoCodeId)
        {
            return BadRequest("Promo code ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdatePromoCode.Response>(result);
    }

    [HttpPost("deactivate/{promoCodeId}")]
    [Permission(Policy.CanDeactivatePromoCode)]
    [ProducesResponseType(typeof(DeactivatePromoCode.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePromoCode(
        string promoCodeId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivatePromoCode.Command(promoCodeId), cancellationToken);
        return HandleResult<DeactivatePromoCode.Response>(result);
    }

    [HttpGet("get-redemptions/{promoCodeId}")]
    [Permission(Policy.CanViewPromoCodes)]
    [ProducesResponseType(typeof(PagedData<PromoCodeRedemptionListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPromoCodeRedemptions(
        string promoCodeId,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new GetPromoCodeRedemptions.Query(promoCodeId, offset, limit),
            cancellationToken);
        return Ok(result);
    }
}
