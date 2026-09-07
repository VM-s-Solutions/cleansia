using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Cleansia.Config.Abstractions;

/// <summary>
/// Shared saved-address action implementations for the customer-facing hosts (Customer Web + Customer
/// Mobile). The two hosts previously carried byte-for-byte identical SavedAddressControllers;
/// the request handling lives here once. Each host keeps a thin controller declaring only its route +
/// host-specific authorization attributes and delegating to these protected cores — no route path,
/// verb, request DTO, or response shape moves.
/// </summary>
public abstract class CustomerSavedAddressControllerBase(IMediator mediator) : CleansiaApiController(mediator)
{
    protected async Task<IActionResult> GetMineCore(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetSavedAddresses.Query(), cancellationToken);
        return HandleResult<IReadOnlyList<SavedAddressDto>>(result);
    }

    protected async Task<IActionResult> AddCore(AddSavedAddress.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SavedAddressDto>(result);
    }

    protected async Task<IActionResult> SetDefaultCore(SetDefaultSavedAddress.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<bool>(result);
    }

    protected async Task<IActionResult> UpdateCore(UpdateSavedAddress.Command command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        return HandleResult<SavedAddressDto>(result);
    }

    protected async Task<IActionResult> DeleteCore(string id, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new DeleteSavedAddress.Command(id), cancellationToken);
        // The T MUST be the command's own response type. `HandleSuccess<T>` matches
        // `BusinessResult<T>` BY T, so asking for `bool` against a
        // `BusinessResult<Response>` matched nothing and fell through to `Ok()` — an
        // empty body, whatever ProducesResponseType said.
        //
        // The same mismatch on Dispute/Create cost a customer their attached photo:
        // the caller was handed no id, so the evidence had nowhere to go. Nothing
        // reads this body yet, which is exactly why it was worth closing before
        // something did.
        return HandleResult<DeleteSavedAddress.Response>(result);
    }
}
