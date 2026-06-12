using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// A successful login resets the failed-attempt counter and any expired lockout
/// (the handler only runs after the validator proved the password), so honest users who fumbled a
/// couple of attempts never accumulate towards a lockout across sessions.
/// </summary>
public class LoginHandlerThrottleResetTests
{
    [Fact]
    public async Task Successful_Login_Resets_The_Failed_Attempt_Counter_And_Lockout()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial
        {
            FailedLoginAttempts = 2,
            LockoutEndsAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateTokenAsync(user, It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));

        var result = await InvokeHandler(
            tokenService.Object, repo.Object, new Login.Command(user.Email, "irrelevant-validated-upstream", true));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndsAt);
    }

    private static async Task<BusinessResult<JwtTokenResponse>> InvokeHandler(
        ITokenService tokenService, IUserRepository repo, Login.Command command)
    {
        var handlerType = typeof(Login).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(
            handlerType!, tokenService, repo, new HostAudienceProvider(JwtAudiences.Customer))!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        return await (Task<BusinessResult<JwtTokenResponse>>)handleMethod!.Invoke(
            handler, [command, CancellationToken.None])!;
    }
}
