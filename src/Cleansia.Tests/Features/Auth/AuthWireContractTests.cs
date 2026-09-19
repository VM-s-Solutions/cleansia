using System.Text.Json;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Enums;
using Cleansia.Infra.Common.Configuration.Interfaces;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Pins the auth wire contract: the server-authoritative fields are projected OFF the request wire so
/// they never reach a generated client, while the client-supplied fields stay ON. The serializer here
/// is the same System.Text.Json the API model-binds with and Swashbuckle builds its schema from, so a
/// field absent from this round-trip is absent from the generated TS/Kotlin client too.
///   - Web Login/PartnerLogin/AdminLogin: trustedDeviceToken is server-set (cookie) → off the wire.
///   - RefreshToken: requiredProfiles/requiredAudience are the host's pin → off the wire.
///   - Mobile Login/PartnerLogin: trustedDeviceToken is client-supplied → on the wire.
/// </summary>
public class AuthWireContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void WebLogin_TrustedDeviceToken_Is_Not_On_The_Wire()
    {
        var json = JsonSerializer.Serialize(
            new Login.Command("e@x.com", "pw", true) { TrustedDeviceToken = "marker" }, Options);

        Assert.DoesNotContain("trustedDeviceToken", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The session acts — the admin sign-in among them — are market-scoped for the audit row's tenant
    /// (explicit interface implementation, always the default market); the seam stays off the wire so
    /// the generated clients do not change.
    /// </summary>
    [Theory]
    [InlineData(typeof(Login.Command))]
    [InlineData(typeof(AdminLogin.Command))]
    [InlineData(typeof(MobileLogin.Command))]
    [InlineData(typeof(ConfirmUserEmail.Command))]
    [InlineData(typeof(Cleansia.Core.AppServices.Features.Users.RequestPasswordChange.Command))]
    [InlineData(typeof(Cleansia.Core.AppServices.Features.Users.ChangePassword.Command))]
    public void The_Session_Acts_Market_Seam_Is_Not_On_The_Wire(Type command)
    {
        Assert.True(typeof(Cleansia.Core.AppServices.Tenancy.IOperatorScopedRequest).IsAssignableFrom(command));
        Assert.DoesNotContain(command.GetProperties(), p => p.Name == "CountryId");
    }

    [Fact]
    public void WebLogin_TrustedDeviceToken_Cannot_Be_Set_From_The_Body()
    {
        const string body = """{"email":"e@x.com","password":"pw","rememberMe":true,"trustedDeviceToken":"forged"}""";

        var command = JsonSerializer.Deserialize<Login.Command>(body, Options)!;

        Assert.Null(command.TrustedDeviceToken);
    }

    [Fact]
    public void WebPartnerLogin_TrustedDeviceToken_Is_Not_On_The_Wire()
    {
        var json = JsonSerializer.Serialize(
            new PartnerLogin.Command("e@x.com", "pw", true) { TrustedDeviceToken = "marker" }, Options);

        Assert.DoesNotContain("trustedDeviceToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WebAdminLogin_TrustedDeviceToken_Is_Not_On_The_Wire()
    {
        var json = JsonSerializer.Serialize(
            new AdminLogin.Command("e@x.com", "pw", true) { TrustedDeviceToken = "marker" }, Options);

        Assert.DoesNotContain("trustedDeviceToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefreshToken_RequiredProfiles_And_RequiredAudience_Are_Not_On_The_Wire()
    {
        var json = JsonSerializer.Serialize(
            new RefreshToken.Command("raw")
            {
                RequiredProfiles = [UserProfile.Customer],
                RequiredAudience = JwtAudiences.Customer,
            },
            Options);

        Assert.DoesNotContain("requiredProfile", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requiredAudience", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RefreshToken_RequiredProfiles_And_RequiredAudience_Cannot_Be_Set_From_The_Body()
    {
        const string body = """{"token":"raw","requiredProfiles":[0],"requiredAudience":"cleansia.admin"}""";

        var command = JsonSerializer.Deserialize<RefreshToken.Command>(body, Options)!;

        Assert.Null(command.RequiredProfiles);
        Assert.Null(command.RequiredAudience);
        Assert.Equal("raw", command.Token);
    }

    [Fact]
    public void MobileLogin_TrustedDeviceToken_Is_On_The_Wire()
    {
        var json = JsonSerializer.Serialize(
            new MobileLogin.Command("e@x.com", "pw", true, "marker"), Options);

        Assert.Contains("trustedDeviceToken", json, StringComparison.OrdinalIgnoreCase);

        var roundTripped = JsonSerializer.Deserialize<MobileLogin.Command>(json, Options)!;
        Assert.Equal("marker", roundTripped.TrustedDeviceToken);
    }

    [Fact]
    public void MobilePartnerLogin_TrustedDeviceToken_Is_On_The_Wire()
    {
        var json = JsonSerializer.Serialize(
            new MobilePartnerLogin.Command("e@x.com", "pw", true, "marker"), Options);

        Assert.Contains("trustedDeviceToken", json, StringComparison.OrdinalIgnoreCase);

        var roundTripped = JsonSerializer.Deserialize<MobilePartnerLogin.Command>(json, Options)!;
        Assert.Equal("marker", roundTripped.TrustedDeviceToken);
    }
}
