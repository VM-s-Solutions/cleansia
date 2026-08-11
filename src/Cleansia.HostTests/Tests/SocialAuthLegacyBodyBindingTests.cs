using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Every shipped Google/Apple client posts a body with no <c>termsAccepted</c> member, and none can
/// grow one until NSwag is regenerated. So the whole change rests on an absent member binding to
/// <c>false</c> rather than being rejected — and per S1 that layer is invisible below the host: MVC's
/// implicit-required runs before MediatR, so a unit or mediator-level test constructs the command
/// directly and cannot see it. An old client must reach the handler and be told what is wrong with
/// its token, not be handed "The TermsAccepted field is required."
/// </summary>
public sealed class SocialAuthLegacyBodyBindingTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    [Fact]
    public async Task A_Google_Body_Without_TermsAccepted_Binds_And_Reaches_The_Handler()
    {
        var response = await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/GoogleAuth", new
        {
            token = "a-token-the-verifier-will-reject",
            googleId = "g-1",
            email = "legacy-client@hosttests.local",
            firstName = "Legacy",
            lastName = "Client",
        });

        await AssertReachedTheHandler(response, BusinessErrorMessage.InvalidGoogleUserToken);
    }

    [Fact]
    public async Task An_Apple_Body_Without_TermsAccepted_Binds_And_Reaches_The_Handler()
    {
        var response = await CustomerClientAnonymous().PostAsJsonAsync("/api/Auth/AppleAuth", new
        {
            identityToken = "a-token-the-verifier-will-reject",
            rawNonce = "raw-nonce",
            firstName = "Legacy",
            lastName = "Client",
        });

        await AssertReachedTheHandler(response, BusinessErrorMessage.InvalidAppleUserToken);
    }

    private static async Task AssertReachedTheHandler(HttpResponseMessage response, string expectedKey)
    {
        var body = await response.Content.ReadAsStringAsync();

        // A model-binding rejection is a 400 whose errors bag names the absent member; the handler's own
        // verdict on an unverifiable token is a 401 carrying the token key. Asserting the key alone is
        // not enough — 400 also carries a bag — so the status is what separates the two layers.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("TermsAccepted", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedKey, body, StringComparison.Ordinal);
    }
}
