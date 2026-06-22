using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Moq;

namespace Cleansia.Tests.Controllers;

/// <summary>
/// The mobile login hosts dispatch the mobile login commands that carry the trusted-device marker in
/// the body (native clients can't read the HttpOnly refresh cookie the web hosts use). These pin that
/// the body value reaches the dispatched command unchanged so the validator's lockout bypass can read
/// it server-side — the web Login/PartnerLogin commands keep that field off the wire.
/// </summary>
public class MobileLoginDispatchTests
{
    private const string BodyTrustedToken = "mobile-body-trusted-device-token";

    private static (Mock<IMediator> mediator, Func<object?> captured) ArrangeMediator()
    {
        var mediator = new Mock<IMediator>();
        object? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<MobileLogin.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = cmd)
            .ReturnsAsync(BusinessResult.Failure<JwtTokenResponse>(
                new Error("Email", BusinessErrorMessage.InvalidPassword)));
        mediator
            .Setup(m => m.Send(It.IsAny<MobilePartnerLogin.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = cmd)
            .ReturnsAsync(BusinessResult.Failure<JwtTokenResponse>(
                new Error("Email", BusinessErrorMessage.InvalidPassword)));
        return (mediator, () => captured);
    }

    [Fact]
    public async Task MobileCustomer_Login_Dispatches_MobileLogin_Carrying_The_Body_TrustedDeviceToken()
    {
        var (mediator, captured) = ArrangeMediator();
        var controller = new Cleansia.Web.Mobile.Customer.Controllers.AuthController(mediator.Object);

        await controller.Login(new MobileLogin.Command("user@example.com", "pw", true, BodyTrustedToken));

        var command = Assert.IsType<MobileLogin.Command>(captured());
        Assert.Equal(BodyTrustedToken, command.TrustedDeviceToken);
    }

    [Fact]
    public async Task MobilePartner_Login_Dispatches_MobilePartnerLogin_Carrying_The_Body_TrustedDeviceToken()
    {
        var (mediator, captured) = ArrangeMediator();
        var controller = new Cleansia.Web.Mobile.Partner.Controllers.AuthController(mediator.Object);

        await controller.Login(new MobilePartnerLogin.Command("user@example.com", "pw", true, BodyTrustedToken));

        var command = Assert.IsType<MobilePartnerLogin.Command>(captured());
        Assert.Equal(BodyTrustedToken, command.TrustedDeviceToken);
    }
}
