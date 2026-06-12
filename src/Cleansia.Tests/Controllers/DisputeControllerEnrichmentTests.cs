using System.Reflection;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Moq;

namespace Cleansia.Tests.Controllers;

/// <summary>
/// ADR-0001 §D2 Note C — the per-host command-construction seam (matching the
/// existing JWT-enrichment idiom <c>command with { ... }</c>). The staff flag is derived by the HOST,
/// not trusted from the body:
///   - the Customer and Mobile.Customer <c>AddMessage</c> controllers force
///     <c>IsStaffMessage = false</c> — a customer can never submit a staff message;
///   - the new Admin <c>AddMessage</c> controller forces <c>IsStaffMessage = true</c>;
///   - the Partner host no longer exposes an <c>AddMessage</c> action at all.
/// </summary>
public class DisputeControllerEnrichmentTests
{
    // The body always asks for staff=true; the host must override it.
    private static AddDisputeMessage.Command ForgedBody() =>
        new(DisputeId: "dispute-1", Message: "x", IsStaffMessage: true);

    [Fact]
    public async Task Customer_Host_AddMessage_Forces_IsStaffMessage_False()
    {
        var mediator = new Mock<IMediator>();
        AddDisputeMessage.Command? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<AddDisputeMessage.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = (AddDisputeMessage.Command)cmd)
            .ReturnsAsync(BusinessResult.Success());

        var controller = new Cleansia.Web.Customer.Controllers.DisputeController(mediator.Object);
        await controller.AddMessage(ForgedBody(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.False(captured!.IsStaffMessage);
    }

    [Fact]
    public async Task MobileCustomer_Host_AddMessage_Forces_IsStaffMessage_False()
    {
        var mediator = new Mock<IMediator>();
        AddDisputeMessage.Command? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<AddDisputeMessage.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = (AddDisputeMessage.Command)cmd)
            .ReturnsAsync(BusinessResult.Success());

        var controller = new Cleansia.Web.Mobile.Customer.Controllers.DisputeController(mediator.Object);
        await controller.AddMessage(ForgedBody(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.False(captured!.IsStaffMessage);
    }

    [Fact]
    public async Task Admin_Host_AddMessage_Forces_IsStaffMessage_True()
    {
        var mediator = new Mock<IMediator>();
        AddDisputeMessage.Command? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<AddDisputeMessage.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = (AddDisputeMessage.Command)cmd)
            .ReturnsAsync(BusinessResult.Success());

        // The admin body deliberately says false; the host must override it to true.
        var body = new AddDisputeMessage.Command("dispute-1", "we are on it", IsStaffMessage: false);
        var controller = new Cleansia.Web.Admin.Controllers.AdminDisputeController(mediator.Object);
        await controller.AddMessage(body, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.True(captured!.IsStaffMessage);
    }

    [Fact]
    public void Partner_Host_Has_No_AddMessage_Action()
    {
        // the staff AddMessage endpoint is gone from the Partner host — no cleaner posts a
        // dispute message of any kind on Partner.
        var method = typeof(Cleansia.Web.Partner.Controllers.DisputeController)
            .GetMethod("AddMessage", BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(method);
    }

    [Theory]
    [InlineData("ResolveDispute")]
    [InlineData("UpdateStatus")]
    [InlineData("CreateDispute")]
    [InlineData("GetDisputeById")]
    [InlineData("GetPagedDisputes")]
    public void Partner_Host_No_Longer_Exposes_The_Migrated_Or_Dead_Dispute_Actions(string actionName)
    {
        // SEC-DSP-07: the admin-policied Resolve/UpdateStatus actions and the duplicated
        // customer-policied Create/GetById/GetPaged are gone from the Partner host. Resolve/UpdateStatus
        // now live on the Admin host; Create/GetById/GetPaged live on the Customer host.
        var method = typeof(Cleansia.Web.Partner.Controllers.DisputeController)
            .GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(method);
    }

    [Theory]
    [InlineData("ResolveDispute")]
    [InlineData("UpdateStatus")]
    [InlineData("GetDisputeById")]
    [InlineData("GetPagedDisputes")]
    public void Admin_Host_Owns_The_Dispute_Management_Actions(string actionName)
    {
        var method = typeof(Cleansia.Web.Admin.Controllers.AdminDisputeController)
            .GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
    }
}
