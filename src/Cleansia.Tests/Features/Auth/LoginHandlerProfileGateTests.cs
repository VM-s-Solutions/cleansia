using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// Pins the observable behavior of the per-host login handlers' profile gate: which
/// <see cref="BusinessErrorMessage"/> + <see cref="Error.Code"/> each returns for an allowed
/// vs a rejected profile. This is the safety net for the login handler consolidation — the gate
/// must stay byte-identical through the refactor.
/// </summary>
public class LoginHandlerProfileGateTests
{
    private const string Audience = JwtAudiences.Customer;

    private static T Invoke<T>(Type featureType, ITokenService tokenService, IUserRepository repo, object command)
    {
        var handlerType = featureType.GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public)!;
        var handler = Activator.CreateInstance(
            handlerType, tokenService, repo, new HostAudienceProvider(Audience))!;
        var handleMethod = handlerType.GetMethod("Handle")!;
        return (T)handleMethod.Invoke(handler, [command, CancellationToken.None])!;
    }

    private static (Mock<ITokenService> tokenService, Mock<IUserRepository> repo) Arrange(User user)
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateTokenAsync(user, It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JwtTokenResponse(Token: "jwt", IsEmailConfirmed: true));
        return (tokenService, repo);
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Administrator)]
    public async Task PartnerLogin_Allows_Employee_And_Administrator(UserProfile profile)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = profile });
        var (tokenService, repo) = Arrange(user);

        var result = await Invoke<Task<BusinessResult<JwtTokenResponse>>>(
            typeof(PartnerLogin), tokenService.Object, repo.Object,
            new PartnerLogin.Command(user.Email, "validated-upstream", true));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PartnerLogin_Rejects_Customer_With_InsufficientPrivileges()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        var (tokenService, repo) = Arrange(user);

        var result = await Invoke<Task<BusinessResult<JwtTokenResponse>>>(
            typeof(PartnerLogin), tokenService.Object, repo.Object,
            new PartnerLogin.Command(user.Email, "validated-upstream", true));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InsufficientPrivileges, result.Error!.Message);
        Assert.Equal(nameof(PartnerLogin.Command.Email), result.Error.Code);
    }

    [Fact]
    public async Task AdminLogin_Allows_Administrator_And_Sets_HasAdminAccess()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Administrator });
        var (tokenService, repo) = Arrange(user);

        var result = await Invoke<Task<BusinessResult<JwtTokenResponse>>>(
            typeof(AdminLogin), tokenService.Object, repo.Object,
            new AdminLogin.Command(user.Email, "validated-upstream", true));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.HasAdminAccess);
    }

    [Theory]
    [InlineData(UserProfile.Employee)]
    [InlineData(UserProfile.Customer)]
    public async Task AdminLogin_Rejects_NonAdministrator_With_InsufficientPrivileges(UserProfile profile)
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = profile });
        var (tokenService, repo) = Arrange(user);

        var result = await Invoke<Task<BusinessResult<JwtTokenResponse>>>(
            typeof(AdminLogin), tokenService.Object, repo.Object,
            new AdminLogin.Command(user.Email, "validated-upstream", true));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InsufficientPrivileges, result.Error!.Message);
        Assert.Equal(nameof(AdminLogin.Command.Email), result.Error.Code);
    }

    [Fact]
    public async Task Login_Allows_Any_Active_Profile()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        var (tokenService, repo) = Arrange(user);

        var result = await Invoke<Task<BusinessResult<JwtTokenResponse>>>(
            typeof(Login), tokenService.Object, repo.Object,
            new Login.Command(user.Email, "validated-upstream", true));

        Assert.True(result.IsSuccess);
    }
}
