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
        // HandleSuccess matches BusinessResult<T> by T; passing bool (not DeleteSavedAddress.Response)
        // intentionally falls through to an empty 200 body — the historical wire shape this surface
        // exposes. Returning the SavedAddressId is a generated-client change for its own ticket.
        return HandleResult<bool>(result);
    }
}
