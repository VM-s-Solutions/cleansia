using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Web.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("CreateOrder")]
    [ProducesResponseType(typeof(CreateOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrder.Command command)
    {
        var result = await Mediator.Send(command);

        return HandleResult<CreateOrder.Response>(result);
    }
}