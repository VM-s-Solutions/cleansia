using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The session acts are anonymous when they run, so each handler names the subject the row is about
/// (<c>ActorUserId</c>) and keys the row on that user; the sign-out runs under a session and keys on it.
/// What each records: the password sign-ins record the method, the lifetime the session was issued
/// with and whether a token was actually minted (an unconfirmed address is a correct password that opened
/// no session); the sign-out records whether there was a token to revoke; the e-mail confirmation records
/// which wire shape confirmed it; the two reset acts record nothing beyond the subject — and none of them
/// carries the address the command was sent with.
/// </summary>
public sealed class SessionAuditEvidenceTests
{
    private const string Password = "validated-upstream";

    private readonly AuditContext _auditContext = new();

    // The internal handlers are built by parameter type; a logger parameter gets the null logger for
    // the handler's own type, which the test cannot name.
    private static object Construct(Type feature, params object[] dependencies)
    {
        var handlerType = feature.GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public)!;
        var constructor = handlerType.GetConstructors().Single();
        return constructor.Invoke(constructor.GetParameters()
            .Select(p => p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>)
                ? typeof(NullLogger<>).MakeGenericType(handlerType).GetField("Instance")!.GetValue(null)!
                : dependencies.Single(d => p.ParameterType.IsInstanceOfType(d)))
            .ToArray());
    }

    private static Task<T> Invoke<T>(object handler, object command) =>
        (Task<T>)handler.GetType().GetMethod("Handle")!.Invoke(handler, [command, CancellationToken.None])!;

    private static (Mock<ITokenService> Tokens, Mock<IUserRepository> Users) Arrange(User user, bool emailConfirmed = true)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var tokens = new Mock<ITokenService>();
        tokens
            .Setup(t => t.GenerateTokenAsync(user, It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: emailConfirmed ? "jwt" : string.Empty, IsEmailConfirmed: emailConfirmed));
        return (tokens, users);
    }

    private JsonElement Payload(AuditSnapshot snapshot) => JsonDocument.Parse(snapshot.AfterJson!).RootElement;

    private AuditSnapshot SnapshotKeyedOn(User user)
    {
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal(user.Id, snapshot.ResourceId);
        Assert.Equal(user.Id, snapshot.ActorUserId);
        Assert.DoesNotContain(user.Email, snapshot.AfterJson ?? string.Empty);
        return snapshot;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_Web_Password_SignIn_Records_The_Method_The_RememberMe_The_Audience_And_Keys_On_The_Account(bool rememberMe)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        var (tokens, users) = Arrange(user);
        var handler = Construct(typeof(Login), tokens.Object, users.Object, new HostAudienceProvider(JwtAudiences.Customer), _auditContext);

        var result = await Invoke<BusinessResult<JwtTokenResponse>>(handler, new Login.Command(user.Email, Password, rememberMe));

        Assert.True(result.IsSuccess);
        var payload = Payload(SnapshotKeyedOn(user));
        Assert.Equal(LoginEvidence.PasswordMethod, payload.GetProperty("method").GetString());
        Assert.Equal(rememberMe, payload.GetProperty("rememberMe").GetBoolean());
        Assert.Equal(JwtAudiences.Customer, payload.GetProperty("clientAudience").GetString());
        Assert.True(payload.GetProperty("emailConfirmed").GetBoolean());
    }

    [Fact]
    public async Task A_Correct_Password_On_An_Unconfirmed_Address_Is_Recorded_As_A_SignIn_That_Opened_No_Session()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        var (tokens, users) = Arrange(user, emailConfirmed: false);
        var handler = Construct(typeof(Login), tokens.Object, users.Object, new HostAudienceProvider(JwtAudiences.Customer), _auditContext);

        var result = await Invoke<BusinessResult<JwtTokenResponse>>(handler, new Login.Command(user.Email, Password, RememberMe: true));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Token);
        Assert.False(Payload(SnapshotKeyedOn(user)).GetProperty("emailConfirmed").GetBoolean());
    }

    [Fact]
    public async Task The_Mobile_Password_SignIn_Records_The_Long_Lifetime_It_Issues_Not_The_Flag_The_Client_Sent()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        var (tokens, users) = Arrange(user);
        var handler = Construct(typeof(MobileLogin), tokens.Object, users.Object, new HostAudienceProvider(JwtAudiences.Customer),
            new TestRequestMetadataProvider(deviceId: "device-1"), _auditContext);

        var result = await Invoke<BusinessResult<JwtTokenResponse>>(handler, new MobileLogin.Command(user.Email, Password, RememberMe: false));

        Assert.True(result.IsSuccess);
        var payload = Payload(SnapshotKeyedOn(user));
        Assert.Equal(LoginEvidence.PasswordMethod, payload.GetProperty("method").GetString());
        Assert.True(payload.GetProperty("rememberMe").GetBoolean());
    }

    [Fact]
    public async Task A_Refused_Password_SignIn_Records_No_Evidence_So_The_Row_Carries_Nothing_Of_The_Command()
    {
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var handler = Construct(typeof(Login), Mock.Of<ITokenService>(), users.Object, new HostAudienceProvider(JwtAudiences.Customer), _auditContext);

        var result = await Invoke<BusinessResult<JwtTokenResponse>>(handler, new Login.Command("nobody@cleansia.test", Password, RememberMe: true));

        Assert.True(result.IsFailure);
        Assert.Null(_auditContext.DrainSnapshot());
    }

    [Theory]
    [InlineData("raw-refresh-token", true)]
    [InlineData("", false)]
    public async Task The_SignOut_Records_Whether_A_Token_Was_There_To_Revoke_Keyed_On_The_Session_User(string token, bool tokenPresented)
    {
        var refreshTokens = new Mock<IRefreshTokenService>();
        var session = new TestUserSessionProvider("cust-1", "cust@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())]);
        var handler = Construct(typeof(Logout), refreshTokens.Object, session, _auditContext);

        var result = await Invoke<BusinessResult>(handler, new Logout.Command(token));

        Assert.True(result.IsSuccess);
        var snapshot = _auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal("cust-1", snapshot.ResourceId);
        Assert.Null(snapshot.ActorUserId);
        Assert.Equal(tokenPresented, Payload(snapshot).GetProperty("tokenPresented").GetBoolean());
        refreshTokens.Verify(s => s.RevokeAsync(token, "logout", It.IsAny<CancellationToken>(), "cust-1"), tokenPresented ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData("123456", ConfirmUserEmail.EmailConfirmationEvidence.OtpMethod)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA", ConfirmUserEmail.EmailConfirmationEvidence.LegacyLinkMethod)]
    public async Task The_Email_Confirmation_Records_Which_Wire_Shape_Confirmed_It_Keyed_On_The_Account(string code, string method)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        var (tokens, users) = Arrange(user);
        users.Setup(r => r.GetByConfirmationCodeIgnoringTenantAsync(code, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var handler = new ConfirmUserEmail.Handler(tokens.Object, users.Object, new HostAudienceProvider(JwtAudiences.Customer), _auditContext);

        var result = await handler.Handle(new ConfirmUserEmail.Command(code, user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(method, Payload(SnapshotKeyedOn(user)).GetProperty("method").GetString());
    }

    [Fact]
    public async Task The_Reset_Request_Records_Only_The_Subject()
    {
        var user = User.CreateWithPassword("reset@cleansia.test", "Password1!@abc", "John", "Doe");
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var handler = new RequestPasswordChange.Handler(users.Object, Mock.Of<IPendingDispatch>(), _auditContext);

        var result = await handler.Handle(new RequestPasswordChange.Command(user.Email), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(SnapshotKeyedOn(user).AfterJson);
    }

    [Fact]
    public async Task The_Reset_Completion_Records_Only_The_Subject()
    {
        var user = User.CreateWithPassword("reset@cleansia.test", "Password1!@abc", "John", "Doe");
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var handler = Construct(typeof(ChangePassword), users.Object, Mock.Of<IRefreshTokenService>(), _auditContext);

        var result = await Invoke<BusinessResult<ChangePassword.Response>>(handler,
            new ChangePassword.Command(user.Email, "New-Password-456!", "123456"));

        Assert.True(result.IsSuccess);
        Assert.Null(SnapshotKeyedOn(user).AfterJson);
    }
}
