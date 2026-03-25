using Cleansia.Core.AppServices.Features.Codes;
using Cleansia.Core.AppServices.Shared.DTOs.Enums;
using Cleansia.Web.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CodeController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("GetOverview")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<Code>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IEnumerable<Code>> GetOverview(CancellationToken cancellationToken)
    {
        return await Mediator.Send(new GetCodeOverview.Request(), cancellationToken);
    }
}