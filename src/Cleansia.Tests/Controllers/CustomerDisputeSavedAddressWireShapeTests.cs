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
/// hosts (Customer Web + Customer Mobile). Two routes return an EMPTY 200 body — CreateDispute and
/// SavedAddress Delete — because the controller asks <c>HandleResult&lt;string&gt;</c> /
/// <c>HandleResult&lt;bool&gt;</c> while the handler returns <c>BusinessResult&lt;Response&gt;</c>, so
/// <c>HandleSuccess&lt;T&gt;</c> never matches the <c>BusinessResult&lt;Response&gt;</c> arm and falls
/// through to a bodyless <c>Ok()</c>. Returning the id instead would change the generated client and is
/// a separate ticket. The remaining routes return an object-bodied 200 carrying the handler value.
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

    [Fact]
    public async Task Customer_CreateDispute_Returns_Empty_200_Body()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new CreateDispute.Response("dispute-1")));
        var controller = new CustomerDispute(mediator.Object);

        var actionResult = await controller.CreateDispute(CreateBody(), CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
        Assert.IsNotType<OkObjectResult>(actionResult);
    }

    [Fact]
    public async Task MobileCustomer_CreateDispute_Returns_Empty_200_Body()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new CreateDispute.Response("dispute-1")));
        var controller = new MobileDispute(mediator.Object);

        var actionResult = await controller.CreateDispute(CreateBody(), CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
        Assert.IsNotType<OkObjectResult>(actionResult);
    }

    [Fact]
    public async Task Customer_DeleteSavedAddress_Returns_Empty_200_Body()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new DeleteSavedAddress.Response("addr-1")));
        var controller = new CustomerSavedAddress(mediator.Object);

        var actionResult = await controller.Delete("addr-1", CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
        Assert.IsNotType<OkObjectResult>(actionResult);
    }

    [Fact]
    public async Task MobileCustomer_DeleteSavedAddress_Returns_Empty_200_Body()
    {
        var mediator = MediatorReturning(BusinessResult.Success(new DeleteSavedAddress.Response("addr-1")));
        var controller = new MobileSavedAddress(mediator.Object);

        var actionResult = await controller.Delete("addr-1", CancellationToken.None);

        Assert.IsType<OkResult>(actionResult);
        Assert.IsNotType<OkObjectResult>(actionResult);
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
