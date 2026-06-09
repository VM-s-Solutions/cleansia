using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Admin.Abstractions;
using Cleansia.Web.Admin.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Admin.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminOrderController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("get-paged")]
    [Permission(Policy.CanViewPagedOrder)]
    [ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPagedOrders([FromQuery] GetPagedOrders.Request request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("details/{orderId}")]
    [Permission(Policy.CanViewOrderDetail)]
    [ProducesResponseType(typeof(OrderItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrderDetails(string orderId, CancellationToken cancellationToken)
    {
        var query = new GetOrderDetails.Query(orderId);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<OrderItem>(result);
    }

    [HttpGet("photos/{orderId}")]
    [Permission(Policy.CanViewOrderPhotos)]
    [ProducesResponseType(typeof(GetOrderPhotos.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrderPhotos(string orderId, CancellationToken cancellationToken)
    {
        var query = new GetOrderPhotos.Query(orderId);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<GetOrderPhotos.Response>(result);
    }

    [HttpPost("cancel")]
    [Permission(Policy.CanAdminCancelOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AdminCancelOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelOrder([FromBody] AdminCancelOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AdminCancelOrder.Response>(result);
    }

    [HttpPost("override-status")]
    [Permission(Policy.CanOverrideOrderStatus)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AdminOverrideOrderStatus.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> OverrideOrderStatus([FromBody] AdminOverrideOrderStatus.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AdminOverrideOrderStatus.Response>(result);
    }

    [HttpPost("reassign")]
    [Permission(Policy.CanReassignOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AdminReassignOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReassignOrder([FromBody] AdminReassignOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AdminReassignOrder.Response>(result);
    }

    [HttpPost("refund")]
    [Permission(Policy.CanRefundOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AdminRefundOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RefundOrder([FromBody] AdminRefundOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AdminRefundOrder.Response>(result);
    }
}
