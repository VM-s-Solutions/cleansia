using Cleansia.Config.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RefreshTokenCmd = Cleansia.Core.AppServices.Features.Auth.RefreshToken;

namespace Cleansia.Tests.Controllers;

/// <summary>
/// ADR-0001 D5 §3 — the per-host refresh call sites enrich the
/// <see cref="RefreshTokenCmd.Command"/> with BOTH <c>RequiredAudience</c> (the host's own) AND
/// <c>RequiredProfile</c> (the profile the host serves). This is the same command-construction seam
/// the JWT-enrichment idiom uses (<c>command with { ... }</c>); the gate itself lives in the handler.
/// Per the frozen mapping:
///   - Web.Customer / Mobile.Customer → (cleansia.customer, Customer)
///   - Web.Partner / Mobile.Partner   → (cleansia.partner, Employee)
///   - Web.Admin                      → (cleansia.admin, Administrator)  (already correct)
/// These capture the enriched command per host, so each host passes its <c>RequiredProfile</c>.
/// </summary>
public class RefreshTokenControllerProfileEnrichmentTests
{
    private static (Mock<IMediator> mediator, Func<RefreshTokenCmd.Command?> captured) ArrangeMediator()
    {
        var mediator = new Mock<IMediator>();
        RefreshTokenCmd.Command? captured = null;
        mediator
            .Setup(m => m.Send(It.IsAny<RefreshTokenCmd.Command>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((cmd, _) => captured = (RefreshTokenCmd.Command)cmd)
            // A failure short-circuits the controller's cookie/CSRF path; the command is already
            // captured, which is all these tests assert.
            .ReturnsAsync(BusinessResult.Failure<JwtTokenResponse>(
                new Error(nameof(RefreshTokenCmd.Command.Token), BusinessErrorMessage.InvalidRefreshToken)));
        return (mediator, () => captured);
    }

    private static AuthCookieWriter CookieWriter() => new(new CsrfTokenService("test-csrf-secret"));

    private static AuthCookieConfig CookieConfig() => new()
    {
        AccessCookieName = "access",
        RefreshCookieName = "refresh",
        RequireSecure = false,
    };

    private static void AttachHttpContext(ControllerBase controller) =>
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

    private static RefreshTokenCmd.Command Body() => new(Token: "raw-token");

    [Fact]
    public async Task WebCustomer_Enriches_RequiredProfile_Customer_And_CustomerAudience()
    {
        var (mediator, captured) = ArrangeMediator();
        var controller = new Cleansia.Web.Customer.Controllers.AuthController(mediator.Object, CookieWriter(), CookieConfig());
        AttachHttpContext(controller);

        await controller.RefreshToken(Body(), CancellationToken.None);

        Assert.NotNull(captured());
        Assert.Equal(UserProfile.Customer, captured()!.RequiredProfile);
        Assert.Equal(JwtAudiences.Customer, captured()!.RequiredAudience);
    }

    [Fact]
    public async Task MobileCustomer_Enriches_RequiredProfile_Customer_And_CustomerAudience()
    {
        var (mediator, captured) = ArrangeMediator();
        var controller = new Cleansia.Web.Mobile.Customer.Controllers.AuthController(mediator.Object);

        await controller.RefreshToken(Body(), CancellationToken.None);

        Assert.NotNull(captured());
        Assert.Equal(UserProfile.Customer, captured()!.RequiredProfile);
        Assert.Equal(JwtAudiences.Customer, captured()!.RequiredAudience);
    }

    [Fact]
    public async Task WebPartner_Enriches_RequiredProfile_Employee_And_PartnerAudience()
    {
        var (mediator, captured) = ArrangeMediator();
        var controller = new Cleansia.Web.Partner.Controllers.AuthController(mediator.Object, CookieWriter(), CookieConfig());
        AttachHttpContext(controller);

        await controller.RefreshToken(Body(), CancellationToken.None);

        Assert.NotNull(captured());
        Assert.Equal(UserProfile.Employee, captured()!.RequiredProfile);
        Assert.Equal(JwtAudiences.Partner, captured()!.RequiredAudience);
    }

    [Fact]
    public async Task MobilePartner_Enriches_RequiredProfile_Employee_And_PartnerAudience()
    {
        var (mediator, captured) = ArrangeMediator();
        var controller = new Cleansia.Web.Mobile.Partner.Controllers.AuthController(mediator.Object);

        await controller.RefreshToken(Body(), CancellationToken.None);

        Assert.NotNull(captured());
        Assert.Equal(UserProfile.Employee, captured()!.RequiredProfile);
        // Mobile.Partner keeps its existing audience pin — the (misleadingly named)
        // JwtAudiences.Mobile constant IS the partner-mobile audience (ADR-0001 D5 §2,
        // rename is out of scope). The ticket only adds RequiredProfile.
        Assert.Equal(JwtAudiences.Mobile, captured()!.RequiredAudience);
    }

    // Admin is unchanged: it already pins Administrator + the Admin audience.
    [Fact]
    public async Task Admin_StillEnriches_RequiredProfile_Administrator_And_AdminAudience()
    {
        var (mediator, captured) = ArrangeMediator();
        var controller = new Cleansia.Web.Admin.Controllers.AdminAuthController(mediator.Object, CookieWriter(), CookieConfig());
        AttachHttpContext(controller);

        await controller.RefreshToken(Body(), CancellationToken.None);

        Assert.NotNull(captured());
        Assert.Equal(UserProfile.Administrator, captured()!.RequiredProfile);
        Assert.Equal(JwtAudiences.Admin, captured()!.RequiredAudience);
    }
}
