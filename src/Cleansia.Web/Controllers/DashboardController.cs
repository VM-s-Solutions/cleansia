using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Abstractions;
using Cleansia.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DashboardController(IMediator mediator) : ApiController(mediator)
{
    /// <summary>
    /// Gets dashboard statistics for the current employee.
    /// Returns all metrics in a single optimized call.
    /// </summary>
    [HttpGet("GetStats")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStats(
        [FromQuery] GetDashboardStats.Query query,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets upcoming orders for the dashboard.
    /// This is kept separate as it returns full order objects, not just counts.
    /// </summary>
    [HttpGet("GetUpcomingOrders")]
    [Permission(Policy.CanViewPagedOrder)]
    [ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<OrderListItem>> GetUpcomingOrders(
        [FromQuery] GetPagedOrders.Request request,
        CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }
}
