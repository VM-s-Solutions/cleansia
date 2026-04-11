using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.FiscalFailures;
using Cleansia.Core.AppServices.Features.FiscalFailures.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminFiscalFailureController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet]
    [Permission(Policy.CanManageFiscalFailures)]
    [ProducesResponseType(typeof(List<FiscalFailureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetFiscalFailures(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetFiscalFailures.Query(), cancellationToken);
        return HandleResult<List<FiscalFailureDto>>(result);
    }

    [HttpPost("{receiptId}/retry")]
    [Permission(Policy.CanManageFiscalFailures)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RetryFiscalRegistration(string receiptId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new RetryFiscalRegistration.Command(receiptId), cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpPost("{receiptId}/acknowledge")]
    [Permission(Policy.CanManageFiscalFailures)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcknowledgeFiscalFailure(string receiptId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AcknowledgeFiscalFailure.Command(receiptId), cancellationToken);
        return HandleResult<object>(result);
    }
}
