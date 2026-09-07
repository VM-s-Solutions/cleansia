using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Disputes.DTOs;
using Cleansia.Core.AppServices.Features.SavedAddresses;
using Cleansia.Core.AppServices.Features.SavedAddresses.DTOs;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using CustomerDispute = Cleansia.Web.Customer.Controllers.DisputeController;
using CustomerSavedAddress = Cleansia.Web.Customer.Controllers.SavedAddressController;
using MobileDispute = Cleansia.Web.Mobile.Customer.Controllers.DisputeController;
using MobileSavedAddress = Cleansia.Web.Mobile.Customer.Controllers.SavedAddressController;

namespace Cleansia.Tests.Controllers;

/// <summary>
/// Pins the on-the-wire success shape of the customer-facing Dispute and SavedAddress routes on both
/// hosts (Customer Web + Customer Mobile).
///
/// <para>CreateDispute used to be pinned here as an EMPTY 200 body: the controller asked
/// <c>HandleResult&lt;string&gt;</c> while the handler returns <c>BusinessResult&lt;Response&gt;</c>, so
/// <c>HandleSuccess&lt;T&gt;</c> never matched the <c>BusinessResult&lt;Response&gt;</c> arm and fell
/// through to a bodyless <c>Ok()</c>. This class called the fix "a separate ticket". Owner,
/// 2026-09-03: that ticket arrived, because a photo attached while FILING a dispute never reached
/// the server — the caller was handed no id to upload the evidence against. It returns the id now,
/// on both hosts, and the generated client was regenerated to match.</para>
///
/// <para>SavedAddress Delete had the same defect — <c>HandleResult&lt;bool&gt;</c> against a
/// <c>BusinessResult&lt;Response&gt;</c> — and was closed in the same pass. Nothing read its body
/// yet, which is exactly why it was worth closing before something did: the identical mismatch on
/// CreateDispute cost a customer their attached photo, and it was invisible until someone needed
/// the value that was never there.</para>

/// <para>SetDefault is NOT the same case and stays a bodyless 200: its command is a plain
/// <c>ICommand</c> with no response type, so there is no value being dropped.</para>
/// </summary>
public class CustomerDisputeSavedAddressWireShapeTests
{
    private static Mock<IMediator> MediatorReturning(BusinessResult result)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<BusinessResult>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mediator;
    }

    private static Mock<IMediator> MediatorReturning<T>(BusinessResult<T> result)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<BusinessResult<T>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mediator;
    }

    private static CreateDispute.Command CreateBody() =>
        new(OrderId: "order-1", Reason: DisputeReason.Other, Description: "x");

    private static SavedAddressDto SampleAddress() =>
        new("addr-1", "Home", "Main St 1", "Prague", "10000", null, "CZ", "Czechia", null, null, true);

    private static AddSavedAddress.Command AddBody() =>
        new("Home", "Main St 1", "Prague", "10000", "CZ", false, 0, 0);

    private static UpdateSavedAddress.Command UpdateBody() =>
        new("addr-1", "Home", "Main St 1", "Prague", "10000", "CZ", 0, 0);

    /// <summary>
    /// The mechanism itself, so this cannot come back by another route. Asking for a T the result is
    /// not silently drops the body: nothing throws, nothing warns, the status is still 200, and the
    /// only symptom is a caller receiving nothing. That is exactly how the dispute id went missing
    /// for as long as it did — and how the SavedAddress Delete body is still going missing.
    /// </summary>
    [Fact]
    public void Asking_HandleResult_For_The_Wrong_Type_Silently_Empties_The_Body()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new CreateDispute.Response("dispute-1")));
        var controller = new WrongTypeController(mediator.Object);

        Assert.IsType<OkResult>(
            controller.Handle<string>(BusinessResult.Success(new CreateDispute.Response("dispute-1"))));
        Assert.IsType<OkObjectResult>(
            controller.Handle<CreateDispute.Response>(
                BusinessResult.Success(new CreateDispute.Response("dispute-1"))));
    }

    private sealed class WrongTypeController(IMediator mediator)
        : Cleansia.Config.Abstractions.CleansiaApiController(mediator)
    {
        public IActionResult Handle<T>(BusinessResult result) => HandleResult<T>(result);
    }

    [Fact]
    public async Task Customer_CreateDispute_Answers_With_The_New_Dispute_Id()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new CreateDispute.Response("dispute-1")));
        var controller = new CustomerDispute(mediator.Object);

        var actionResult = await controller.CreateDispute(CreateBody(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<CreateDispute.Response>(ok.Value);
        Assert.Equal("dispute-1", body.DisputeId);
    }

    [Fact]
    public async Task MobileCustomer_CreateDispute_Answers_With_The_New_Dispute_Id()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new CreateDispute.Response("dispute-1")));
        var controller = new MobileDispute(mediator.Object);

        var actionResult = await controller.CreateDispute(CreateBody(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<CreateDispute.Response>(ok.Value);
        Assert.Equal("dispute-1", body.DisputeId);
    }

    [Fact]
    public async Task Customer_DeleteSavedAddress_Answers_With_The_Deleted_Id()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new DeleteSavedAddress.Response("addr-1")));
        var controller = new CustomerSavedAddress(mediator.Object);

        var actionResult = await controller.Delete("addr-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<DeleteSavedAddress.Response>(ok.Value);
        Assert.Equal("addr-1", body.SavedAddressId);
    }

    [Fact]
    public async Task MobileCustomer_DeleteSavedAddress_Answers_With_The_Deleted_Id()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new DeleteSavedAddress.Response("addr-1")));
        var controller = new MobileSavedAddress(mediator.Object);

        var actionResult = await controller.Delete("addr-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        var body = Assert.IsType<DeleteSavedAddress.Response>(ok.Value);
        Assert.Equal("addr-1", body.SavedAddressId);
    }

    [Fact]
    public async Task Customer_AddSavedAddress_Returns_Object_200_Body()
    {
        var dto = SampleAddress();
        var mediator = MediatorReturning(BusinessResult.Success(dto));
        var controller = new CustomerSavedAddress(mediator.Object);

        var actionResult = await controller.Add(AddBody(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task Customer_UpdateSavedAddress_Returns_Object_200_Body()
    {
        var dto = SampleAddress();
        var mediator = MediatorReturning(BusinessResult.Success(dto));
        var controller = new CustomerSavedAddress(mediator.Object);

        var actionResult = await controller.Update(UpdateBody(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Same(dto, ok.Value);
    }

    [Fact]
    public async Task Customer_SetDefaultSavedAddress_Returns_Empty_200_Body()
    {
        // The handler returns a non-generic BusinessResult, so HandleResult<bool> never matches the
        // BusinessResult<bool> arm and falls through to an empty Ok() — unchanged by the refactor.
        var mediator = MediatorReturning(BusinessResult.Success());
        var controller = new CustomerSavedAddress(mediator.Object);

        var actionResult = await controller.SetDefault(new SetDefaultSavedAddress.Command("addr-1"), CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
        Assert.IsNotType<OkObjectResult>(actionResult);
    }
}
