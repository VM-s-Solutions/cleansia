using Cleansia.Core.AppServices.Authentication;
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
public class OrderController(IMediator mediator) : MobileApiController(mediator)
{
    [HttpGet("GetPaged")]
    [Permission(Policy.CanViewPagedOrder)]
    [ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<OrderListItem>> GetPaged([FromQuery] GetPagedOrders.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetById")]
    [Permission(Policy.CanViewOrderDetail)]
    [ProducesResponseType(typeof(OrderItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById([FromQuery] GetOrderDetails.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<OrderItem>(result);
    }

    [HttpPost("TakeOrder")]
    [Permission(Policy.CanTakeOrder)]
    [ProducesResponseType(typeof(TakeOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TakeOrder([FromBody] TakeOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<TakeOrder.Response>(result);
    }

    [HttpPost("StartOrder")]
    [Permission(Policy.CanStartOrder)]
    [ProducesResponseType(typeof(StartOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartOrder([FromBody] StartOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<StartOrder.Response>(result);
    }

    [HttpPost("CompleteOrder")]
    [Permission(Policy.CanCompleteOrder)]
    [ProducesResponseType(typeof(CompleteOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CompleteOrder([FromBody] CompleteOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CompleteOrder.Response>(result);
    }

    [HttpGet("DownloadReceipt")]
    [Permission(Policy.CanViewOrderDetail)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadReceipt([FromQuery] DownloadOrderReceipt.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return HandleResult<DownloadOrderReceipt.Response>(result);
        }

        return File(result.Value!.PdfBytes, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("UploadPhoto")]
    [Permission(Policy.CanUploadOrderPhoto)]
    [ProducesResponseType(typeof(UploadOrderPhoto.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadPhoto([FromBody] UploadOrderPhoto.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UploadOrderPhoto.Response>(result);
    }

    [HttpPost("SavePhotos")]
    [Permission(Policy.CanUploadOrderPhoto)]
    [ProducesResponseType(typeof(SaveOrderPhotos.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SavePhotos([FromBody] SaveOrderPhotos.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SaveOrderPhotos.Response>(result);
    }

    [HttpGet("GetPhotos")]
    [Permission(Policy.CanViewOrderPhotos)]
    [ProducesResponseType(typeof(GetOrderPhotos.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPhotos([FromQuery] GetOrderPhotos.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<GetOrderPhotos.Response>(result);
    }

    [HttpDelete("DeletePhoto")]
    [Permission(Policy.CanDeleteOrderPhoto)]
    [ProducesResponseType(typeof(DeleteOrderPhoto.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePhoto([FromQuery] DeleteOrderPhoto.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<DeleteOrderPhoto.Response>(result);
    }

    [HttpPost("AddNote")]
    [Permission(Policy.CanAddOrderNote)]
    [ProducesResponseType(typeof(AddOrderNote.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddNote([FromBody] AddOrderNote.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AddOrderNote.Response>(result);
    }

    [HttpPost("ReportIssue")]
    [Permission(Policy.CanReportOrderIssue)]
    [ProducesResponseType(typeof(ReportOrderIssue.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReportIssue([FromBody] ReportOrderIssue.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ReportOrderIssue.Response>(result);
    }
}
