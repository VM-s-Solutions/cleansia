using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Currencies;
using Cleansia.Core.AppServices.Features.Currencies.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminCurrencyController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-overview")]
    [Permission(Policy.CanViewCurrencies)]
    [ProducesResponseType(typeof(IEnumerable<CurrencyListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IEnumerable<CurrencyListItem>> GetCurrencies(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCurrencyOverview.Request(), cancellationToken);
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

    [HttpPost]
    [Permission(Policy.CanCreateCurrency)]
    [ProducesResponseType(typeof(CreateCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCurrency(
        [FromBody] CreateCurrency.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateCurrency.Response>(result);
    }

    [HttpPut("{currencyId}")]
    [Permission(Policy.CanUpdateCurrency)]
    [ProducesResponseType(typeof(UpdateCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    [HttpDelete("{currencyId}")]
    [Permission(Policy.CanDeleteCurrency)]
    [ProducesResponseType(typeof(DeleteCurrency.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCurrency(
        string currencyId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteCurrency.Command(currencyId), cancellationToken);
        return HandleResult<DeleteCurrency.Response>(result);
    }
}