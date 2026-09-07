using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Credit;
using Cleansia.Web.Mobile.Customer.Abstractions;
using Cleansia.Web.Mobile.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CreditController(IMediator mediator) : CustomerMobileApiController(mediator)
{
    [HttpGet("GetMy")]
    [Permission(Policy.CanViewMyCredit)]
    [ProducesResponseType(typeof(GetMyCredit.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMy(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyCredit.Query(), cancellationToken);
        return HandleResult<GetMyCredit.Response>(result);
    }
}
