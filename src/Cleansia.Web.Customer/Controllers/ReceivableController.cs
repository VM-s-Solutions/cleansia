using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Receivables;
using Cleansia.Core.AppServices.Features.Receivables.DTOs;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReceivableController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpGet("GetMine")]
    [Permission(Policy.CanManageSavedCard)]
    [ProducesResponseType(typeof(IReadOnlyList<MyReceivableDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyReceivables.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<MyReceivableDto>>(result);
    }

    [HttpPost("CreatePayLink/{id}")]
    [Permission(Policy.CanManageSavedCard)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(CreateReceivablePayLink.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePayLink(string id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateReceivablePayLink.Command(id), cancellationToken);
        return HandleResult<CreateReceivablePayLink.Response>(result);
    }
}
