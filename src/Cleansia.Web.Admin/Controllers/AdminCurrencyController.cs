using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminCurrencyController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-overview")]
    [Permission(Policy.CanViewCurrencies)]
    [ProducesResponseType(typeof(IEnumerable<AdminCurrencyListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IEnumerable<AdminCurrencyListItem>> GetCurrencies(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetAdminCurrencyOverview.Request(), cancellationToken);
    }

    [HttpGet("details/{currencyId}")]
    [Permission(Policy.CanViewCurrencies)]
    [ProducesResponseType(typeof(CurrencyDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrencyById(
        string currencyId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetCurrencyById.Query(currencyId), cancellationToken);
        return HandleResult<CurrencyDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreateCurrency)]
    [ProducesResponseType(typeof(CreateCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> CreateCurrency(
        [FromBody] CreateCurrency.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateCurrency.Response>(result);
    }

    [HttpPut("update/{currencyId}")]
    [Permission(Policy.CanUpdateCurrency)]
    [ProducesResponseType(typeof(UpdateCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> UpdateCurrency(
        string currencyId,
        [FromBody] UpdateCurrency.Command command,
        CancellationToken cancellationToken)
    {
        if (command.CurrencyId != currencyId)
        {
            return BadRequest("Currency ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdateCurrency.Response>(result);
    }

    [HttpPost("set-default/{currencyId}")]
    [Permission(Policy.CanUpdateCurrency)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(SetDefaultCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefaultCurrency(
        string currencyId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SetDefaultCurrency.Command(currencyId), cancellationToken);
        return HandleResult<SetDefaultCurrency.Response>(result);
    }

    [HttpPost("deactivate/{currencyId}")]
    [Permission(Policy.CanUpdateCurrency)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeactivateCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateCurrency(string currencyId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivateCurrency.Command(currencyId), cancellationToken);
        return HandleResult<DeactivateCurrency.Response>(result);
    }

    [HttpPost("activate/{currencyId}")]
    [Permission(Policy.CanUpdateCurrency)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ActivateCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateCurrency(string currencyId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ActivateCurrency.Command(currencyId), cancellationToken);
        return HandleResult<ActivateCurrency.Response>(result);
    }

    [HttpDelete("delete/{currencyId}")]
    [Permission(Policy.CanDeleteCurrency)]
    [ProducesResponseType(typeof(DeleteCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteCurrency(
        string currencyId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteCurrency.Command(currencyId), cancellationToken);
        return HandleResult<DeleteCurrency.Response>(result);
    }
}