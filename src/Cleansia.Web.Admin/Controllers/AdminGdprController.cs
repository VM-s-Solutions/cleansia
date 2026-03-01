using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class AdminGdprController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("export/{userId}")]
    [Permission(Policy.CanAdminExportUserData)]
    [ProducesResponseType(typeof(GdprExportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportUserData(string userId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AdminExportUserData.Query(userId), cancellationToken);
        return HandleResult<GdprExportDto>(result);
    }

    [HttpPost("delete-account/{userId}")]
    [Permission(Policy.CanAdminDeleteUserAccount)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteUserAccount(string userId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AdminDeleteUserAccount.Command(userId), cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpGet("consents/{userId}")]
    [Permission(Policy.CanAdminViewUserConsents)]
    [ProducesResponseType(typeof(List<UserConsentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserConsents(string userId, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new AdminGetUserConsents.Query(userId), cancellationToken);
        return HandleResult<List<UserConsentDto>>(result);
    }

    [HttpGet("requests")]
    [Permission(Policy.CanViewGdprRequests)]
    [ProducesResponseType(typeof(List<GdprRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllGdprRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(new GetAllGdprRequests.Query(page, pageSize), cancellationToken);
        return HandleResult<List<GdprRequestDto>>(result);
    }
}
