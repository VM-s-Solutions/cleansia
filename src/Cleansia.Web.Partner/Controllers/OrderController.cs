using Cleansia.Config.Filters;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Web.Partner.Abstractions;
using Cleansia.Web.Partner.Attributes;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cleansia.Web.Partner.Controllers;

[Route("api/[controller]")]
[ApiController]
[RequireCompleteProfile]
public class OrderController(IMediator mediator) : ApiController(mediator)
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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(StartOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartOrder([FromBody] StartOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<StartOrder.Response>(result);
    }

    /// <summary>
    /// Cleaner taps "On my way" � fires a heads-up push to the customer
    /// and appends an OnTheWay status track. Optional step in the
    /// workflow (Confirmed ? OnTheWay ? InProgress); cleaner can still
    /// skip directly from Confirmed ? InProgress via StartOrder if they
    /// don't want to send the heads-up.
    /// </summary>
    [HttpPost("NotifyOnTheWay")]
    [Permission(Policy.CanStartOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(NotifyOnTheWay.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> NotifyOnTheWay([FromBody] NotifyOnTheWay.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<NotifyOnTheWay.Response>(result);
    }

    [HttpPost("CompleteOrder")]
    [Permission(Policy.CanCompleteOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(CompleteOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CompleteOrder([FromBody] CompleteOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<CompleteOrder.Response>(result);
    }

    [HttpPost("MarkCashCollected")]
    [Permission(Policy.CanCompleteOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(MarkCashCollected.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkCashCollected([FromBody] MarkCashCollected.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<MarkCashCollected.Response>(result);
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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ReportOrderIssue.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReportIssue([FromBody] ReportOrderIssue.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<ReportOrderIssue.Response>(result);
    }

    // ADR-0045 D9 — "jobs waiting for your answer": the orders reserved for this cleaner alone until
    // their deadline. Four existing conjuncts and one equality; no new predicate anywhere.
    [HttpGet("MyPendingOffers")]
    [Permission(Policy.CanViewPagedOrder)]
    [ProducesResponseType(typeof(IReadOnlyList<PendingOfferItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MyPendingOffers(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetMyPendingOffers.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<PendingOfferItem>>(result);
    }

    // The other half of Confirm. "Confirm" is TakeOrder, unchanged, with a different label — a second
    // acquisition path would either duplicate TakeOrder's ordered chain or be weaker than it.
    [HttpPost("DeclinePreferredOffer")]
    [Permission(Policy.CanTakeOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeclinePreferredOffer.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeclinePreferredOffer(
        [FromBody] DeclinePreferredOffer.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<DeclinePreferredOffer.Response>(result);
    }

    // The cleaner cannot make it, but does not want to leave the job uncovered. They stay ASSIGNED and
    // obliged until somebody takes it — the seat becomes takeable, not empty. Owner ruling 2026-09-06.
    [HttpPost("RequestCover")]
    [Permission(Policy.CanTakeOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(RequestCover.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RequestCover(
        [FromBody] RequestCover.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<RequestCover.Response>(result);
    }

    // The way out when nobody answers. Releases the seat outright; the booking is NOT cancelled and no
    // money moves here — an order that reaches its slot with nobody on it is the sweep's business.
    //
    // Same permission as taking a job, deliberately: Policy.CanTakeOrder already means "may act on
    // their own assignments", DeclinePreferredOffer ships under it for the same reason, and a policy
    // of its own would map to the same physical policy and distinguish nothing.
    [HttpPost("DropOrder")]
    [Permission(Policy.CanTakeOrder)]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DropOrder.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DropOrder(
        [FromBody] DropOrder.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<DropOrder.Response>(result);
    }

    // The contract for work a cleaner reads before taking a job (ADR-0068 D3): the order's own text,
    // the job facts the acceptance will freeze, and the text-row id the take must echo.
    [HttpGet("GetWorkContractPreview")]
    [Permission(Policy.CanTakeOrder)]
    [EnableRateLimiting("interactive")]
    [ProducesResponseType(typeof(WorkContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWorkContractPreview([FromQuery] GetWorkContractPreview.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<WorkContractDto>(result);
    }

    // The standalone acceptance for a seat an administrator formed: the same act the take performs
    // inline, under the same permission, for a cleaner who was placed rather than took.
    [HttpPost("AcceptWorkContract")]
    [Permission(Policy.CanTakeOrder)]
    [EnableRateLimiting("interactive")]
    [ProducesResponseType(typeof(AcceptWorkContract.Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AcceptWorkContract([FromBody] AcceptWorkContract.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<AcceptWorkContract.Response>(result);
    }

    // The accepted contract for work, keyed on the acceptance (ADR-0068 D4): the order's customer, the
    // cleaner who accepted it and an administrator read it; anyone else answers order.not_found.
    [HttpGet("GetWorkContract")]
    [Permission(Policy.CanViewOrderDetail)]
    [EnableRateLimiting("interactive")]
    [ProducesResponseType(typeof(WorkContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetWorkContract([FromQuery] GetWorkContract.Query query, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<WorkContractDto>(result);
    }
}
