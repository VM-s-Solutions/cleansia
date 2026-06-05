using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Mobile.Partner.Abstractions;
using Cleansia.Web.Mobile.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Web.Mobile.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DashboardController(IMediator mediator) : MobileApiController(mediator)
{
    [HttpGet("GetStats")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStats([FromQuery] GetDashboardStats.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<DashboardStatsDto>(result);
    }

    [HttpGet("GetAvailableJobsPreview")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(AvailableJobsPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAvailableJobsPreview([FromQuery] GetAvailableJobsPreview.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<AvailableJobsPreviewResponse>(result);
    }

    [HttpGet("GetUpcomingOrders")]
    [Permission(Policy.CanViewPagedOrder)]
    [ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<OrderListItem>> GetUpcomingOrders([FromQuery] GetPagedOrders.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetEarningsAnalytics")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(EarningsAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEarningsAnalytics([FromQuery] GetEarningsAnalytics.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<EarningsAnalyticsDto>(result);
    }

    [HttpGet("GetTimeAnalytics")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(TimeAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTimeAnalytics([FromQuery] GetTimeAnalytics.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<TimeAnalyticsDto>(result);
    }

    [HttpGet("GetOrderAnalytics")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(OrderAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrderAnalytics([FromQuery] GetOrderAnalytics.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<OrderAnalyticsDto>(result);
    }

    [HttpGet("GetProductivityMetrics")]
    [Permission(Policy.CanGetCurrentEmployee)]
    [ProducesResponseType(typeof(ProductivityMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProductivityMetrics([FromQuery] GetProductivityMetrics.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<ProductivityMetricsDto>(result);
    }
}
