using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.FeatureFlags;
using Cleansia.Core.AppServices.Features.FeatureFlags.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminFeatureFlagController(IMediator mediator, ITenantProvider tenantProvider) : ApiController(mediator)
{
    [HttpGet]
    [Permission(Policy.CanViewFeatureFlags)]
    [ProducesResponseType(typeof(List<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? scope, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetAllFeatureFlags.Query(scope), cancellationToken);
        return HandleResult<List<FeatureFlagDto>>(result);
    }

    [HttpPost]
    [Permission(Policy.CanCreateFeatureFlag)]
    [ProducesResponseType(typeof(CreateFeatureFlag.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateFeatureFlag.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CreateFeatureFlag.Response>(result);
    }

    [HttpPost("{id}/toggle")]
    [Permission(Policy.CanToggleFeatureFlag)]
    [ProducesResponseType(typeof(ToggleFeatureFlag.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> Toggle(string id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new ToggleFeatureFlag.Command(id), cancellationToken);
        return HandleResult<ToggleFeatureFlag.Response>(result);
    }

    [HttpDelete("{id}")]
    [Permission(Policy.CanDeleteFeatureFlag)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteFeatureFlag.Command(id), cancellationToken);
        return HandleResult<object>(result);
    }

    [HttpGet("check")]
    [Permission(Policy.CanViewFeatureFlags)]
    [ProducesResponseType(typeof(CheckFeatureFlag.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> Check([FromQuery] string featureName, [FromQuery] string? countryId, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var result = await Mediator.Send(new CheckFeatureFlag.Query(featureName, countryId, tenantId), cancellationToken);
        return HandleResult<CheckFeatureFlag.Response>(result);
    }
}
