using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Customer.Abstractions;
using Cleansia.Web.Customer.Attributes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Customer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrderController(IMediator mediator) : CustomerApiController(mediator)
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpGet("Lookup")]
    [ProducesResponseType(typeof(LookupOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LookupOrder([FromQuery] string orderNumber, [FromQuery] string email, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new LookupOrder.Query(orderNumber, email), cancellationToken);
        return HandleResult<LookupOrder.Response>(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("LookupBatch")]
    [ProducesResponseType(typeof(LookupOrderBatch.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> LookupOrderBatch([FromBody] LookupOrderBatch.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<LookupOrderBatch.Response>(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("CreateOrder")]
    [ProducesResponseType(typeof(CreateOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrder.Command command)
    {
        var result = await Mediator.Send(command);
        return HandleResult<CreateOrder.Response>(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("Quote")]
    [ProducesResponseType(typeof(QuoteOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Quote([FromBody] QuoteOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<QuoteOrder.Response>(result);
    }

    /// <summary>
    /// Customer-driven confirmation of a Pending recurring-template Order.
    /// Cash returns success immediately (order flips to Confirmed + Paid).
    /// Card returns a Stripe PaymentIntent + ephemeral key so the mobile
    /// PaymentSheet can collect payment.
    /// </summary>
    [Authorize]
    [HttpPost("ConfirmRecurring")]
    [ProducesResponseType(typeof(ConfirmRecurringOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmRecurring([FromBody] ConfirmRecurringOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ConfirmRecurringOrder.Response>(result);
    }

    [HttpGet("GetMyOrders")]
    [Permission(Policy.CanViewPagedUserOrder)]
    [ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<OrderListItem>> GetMyOrders([FromQuery] GetCustomerOrders.Request request, CancellationToken cancellationToken)
    {
        return await Mediator.Send(request, cancellationToken);
    }

    [HttpGet("GetPaged")]
    [Permission(Policy.CanViewPagedUserOrder)]
    [ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedData<OrderListItem>> GetPaged([FromQuery] GetCustomerOrders.Request request, CancellationToken cancellationToken)
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

    [HttpPost("SubmitReview")]
    [Permission(Policy.CanSubmitOrderReview)]
    [ProducesResponseType(typeof(OrderReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitReview([FromBody] SubmitOrderReview.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<OrderReviewDto>(result);
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

    [HttpPost("Cancel")]
    [Permission(Policy.CanCancelOrder)]
    [ProducesResponseType(typeof(CancelOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelOrder([FromBody] CancelOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CancelOrder.Response>(result);
    }

    [HttpGet("MyServingCleaners")]
    [Permission(Policy.CanViewPagedUserOrder)]
    [ProducesResponseType(typeof(IReadOnlyList<GetMyServingCleaners.Response>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MyServingCleaners(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyServingCleaners.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<GetMyServingCleaners.Response>>(result);
    }
}
