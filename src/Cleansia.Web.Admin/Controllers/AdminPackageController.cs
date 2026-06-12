using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminPackageController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewPackages)]
    [ProducesResponseType(typeof(PagedData<PackageListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedPackages(
        [FromQuery] GetPagedPackages.Request request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{packageId}")]
    [Permission(Policy.CanViewPackages)]
    [ProducesResponseType(typeof(AdminPackageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPackageById(
        string packageId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPackageById.Query(packageId), cancellationToken);
        return HandleResult<AdminPackageDetailDto>(result);
    }

    [HttpPost("create")]
    [Permission(Policy.CanCreatePackage)]
    [ProducesResponseType(typeof(CreatePackage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreatePackage(
        [FromBody] CreatePackage.Command command,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreatePackage.Response>(result);
    }

    [HttpPut("update/{packageId}")]
    [Permission(Policy.CanUpdatePackage)]
    [ProducesResponseType(typeof(UpdatePackage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePackage(
        string packageId,
        [FromBody] UpdatePackage.Command command,
        CancellationToken cancellationToken)
    {
        if (command.PackageId != packageId)
        {
            return BadRequest("Package ID in route does not match command");
        }
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UpdatePackage.Response>(result);
    }

    [HttpPost("deactivate/{packageId}")]
    [Permission(Policy.CanUpdatePackage)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeactivatePackage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivatePackage(
        string packageId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeactivatePackage.Command(packageId), cancellationToken);
        return HandleResult<DeactivatePackage.Response>(result);
    }

    [HttpPost("activate/{packageId}")]
    [Permission(Policy.CanUpdatePackage)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ActivatePackage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivatePackage(
        string packageId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ActivatePackage.Command(packageId), cancellationToken);
        return HandleResult<ActivatePackage.Response>(result);
    }

    [HttpDelete("delete/{packageId}")]
    [Permission(Policy.CanDeletePackage)]
    [ProducesResponseType(typeof(DeletePackage.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePackage(
        string packageId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeletePackage.Command(packageId), cancellationToken);
        return HandleResult<DeletePackage.Response>(result);
    }
}