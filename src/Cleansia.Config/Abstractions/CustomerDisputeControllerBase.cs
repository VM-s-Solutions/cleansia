using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// Shared dispute action implementations for the customer-facing hosts (Customer Web + Customer
/// Mobile). The two hosts previously carried byte-for-byte identical DisputeControllers; the
/// request handling now lives here once, so a fix to one (e.g. the inline upload guard) lands on both.
/// Each host keeps a thin controller declaring only its route + host-specific authorization attributes
/// and delegating to these protected cores — no route path, verb, request DTO, or response shape moves.
/// </summary>
public abstract class CustomerDisputeControllerBase(IMediator mediator) : CleansiaApiController(mediator)
{
    protected async Task<IActionResult> CreateDisputeCore(CreateDispute.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        // HandleSuccess matches BusinessResult<T> by T; passing string (not CreateDispute.Response)
        // intentionally falls through to an empty 200 body — the historical wire shape this surface
        // exposes. Changing it to return the DisputeId is a generated-client change handled in its
        // own ticket, not here.
        return HandleResult<string>(result);
    }

    protected async Task<IActionResult> GetDisputeByIdCore(string disputeId, CancellationToken cancellationToken)
    {
        var query = new GetDisputeDetails.Query(disputeId);
        var result = await Mediator.Send(query, cancellationToken);
        return HandleResult<DisputeDetails>(result);
    }

    protected Task<PagedData<DisputeListItem>> GetPagedDisputesCore(GetPagedDisputes.Request request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request, cancellationToken);
    }

    protected async Task<IActionResult> AddMessageCore(AddDisputeMessage.Command command, CancellationToken cancellationToken)
    {
        // The staff flag is host-derived, not body-supplied. A customer never authors a
        // staff message — force IsStaffMessage=false (mirrors the JWT-enrichment `command with` idiom).
        var enriched = command with { IsStaffMessage = false };
        var result = await Mediator.Send(enriched, cancellationToken);
        return HandleResult<object>(result);
    }

    protected async Task<IActionResult> UploadEvidenceCore(string disputeId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "File is required." });
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);

        var command = new UploadDisputeEvidence.Command(
            DisputeId: disputeId,
            FileName: file.FileName,
            ContentType: file.ContentType,
            FileData: ms.ToArray());

        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<UploadDisputeEvidence.Response>(result);
    }
}
