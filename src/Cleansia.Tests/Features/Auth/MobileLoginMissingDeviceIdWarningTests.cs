using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Extensions;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Evidence gate for a future required-X-Device-Id login validator: a mobile-host login that arrives
/// WITHOUT the X-Device-Id header logs a Warning (host/audience only — never the subject email/PII, S6);
/// a login that carries the header does not. No hard requirement is added yet — claim-less tokens still
/// pass the device directory by design during transition. Covers both mobile login handlers.
/// </summary>
public class MobileLoginMissingDeviceIdWarningTests
{
    private const string Password = "Passw0rd!";
    private const string Audience = JwtAudiences.Mobile;

    private static User ActiveUser(UserProfile profile)
        => UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            Password = Password.HashAndSaltPassword(),
            Profile = profile,
        });

    private static (Mock<ITokenService> tokens, Mock<IUserRepository> users) Arrange(User user)
    {
        var tokens = new Mock<ITokenService>();
        tokens.Setup(s => s.GenerateTokenAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "access", IsEmailConfirmed: true));

        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByEmailIgnoringTenantAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return (tokens, users);
    }

    private static Mock<IRequestMetadataProvider> Metadata(string? deviceId)
    {
        var meta = new Mock<IRequestMetadataProvider>();
        meta.SetupGet(m => m.DeviceId).Returns(deviceId);
        meta.SetupGet(m => m.DeviceLabel).Returns((string?)null);
        meta.SetupGet(m => m.IpAddress).Returns((string?)null);
        return meta;
    }

    private static async Task<List<(LogLevel Level, string Message)>> InvokeAsync(Type featureType, User user, string? deviceId)
    {
        var (tokens, users) = Arrange(user);
        var entries = new List<(LogLevel Level, string Message)>();

        var handlerType = featureType.GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public)!;
        var loggerType = typeof(CapturingLogger<>).MakeGenericType(handlerType);
        var logger = Activator.CreateInstance(loggerType, entries)!;

        var handler = Activator.CreateInstance(
            handlerType,
            tokens.Object,
            users.Object,
            new HostAudienceProvider(Audience),
            Metadata(deviceId).Object,
            logger)!;

        var command = Activator.CreateInstance(
            featureType.GetNestedType("Command")!,
            user.Email, Password, true, null)!;

        var handleMethod = handlerType.GetMethod("Handle")!;
        await (Task<BusinessResult<JwtTokenResponse>>)handleMethod.Invoke(handler, [command, CancellationToken.None])!;
        return entries;
    }

    [Fact]
    public async Task MobileLogin_Without_DeviceId_Header_Logs_A_Warning()
    {
        var user = ActiveUser(UserProfile.Customer);

        var entries = await InvokeAsync(typeof(MobileLogin), user, deviceId: null);

        var warning = Assert.Single(entries.Where(e => e.Level == LogLevel.Warning));
        Assert.Contains("X-Device-Id", warning.Message);
        Assert.Contains(Audience, warning.Message);
        Assert.DoesNotContain(user.Email, warning.Message);
    }

    [Fact]
    public async Task MobileLogin_With_DeviceId_Header_Does_Not_Log_A_Warning()
    {
        var user = ActiveUser(UserProfile.Customer);

        var entries = await InvokeAsync(typeof(MobileLogin), user, deviceId: "android-id-xyz");

        Assert.DoesNotContain(entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task MobilePartnerLogin_Without_DeviceId_Header_Logs_A_Warning()
    {
        var user = ActiveUser(UserProfile.Employee);

        var entries = await InvokeAsync(typeof(MobilePartnerLogin), user, deviceId: null);

        var warning = Assert.Single(entries.Where(e => e.Level == LogLevel.Warning));
        Assert.Contains("X-Device-Id", warning.Message);
        Assert.DoesNotContain(user.Email, warning.Message);
    }

    [Fact]
    public async Task MobilePartnerLogin_With_DeviceId_Header_Does_Not_Log_A_Warning()
    {
        var user = ActiveUser(UserProfile.Employee);

        var entries = await InvokeAsync(typeof(MobilePartnerLogin), user, deviceId: "android-id-xyz");

        Assert.DoesNotContain(entries, e => e.Level == LogLevel.Warning);
    }

    private sealed class CapturingLogger<T>(List<(LogLevel Level, string Message)> entries) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
