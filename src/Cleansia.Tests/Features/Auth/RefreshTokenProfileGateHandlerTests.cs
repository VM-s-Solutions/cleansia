using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Users;
using Moq;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;
using RefreshTokenCmd = Cleansia.Core.AppServices.Features.Auth.RefreshToken;

namespace Cleansia.Tests.Features.Auth;

public class RefreshTokenProfileGateHandlerTests
{
    private const string CustomerAudience = JwtAudiences.Customer;

    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly Mock<IJwtSettings> _jwtSettings = new();

    public RefreshTokenProfileGateHandlerTests()
    {
        _jwtSettings.SetupGet(s => s.Secret).Returns(new string('k', 64));
        _jwtSettings.SetupGet(s => s.Issuer).Returns("cleansia.tests");
        _jwtSettings.SetupGet(s => s.AccessTokenExpMinutes).Returns(15);
    }

    private async Task<BusinessResult<JwtTokenResponse>> Handle(RefreshTokenCmd.Command command)
    {
        var handlerType = typeof(RefreshTokenCmd).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(
            handlerType,
            _refreshTokenService.Object,
            _userRepository.Object,
            _employeeRepository.Object,
            _requestMetadata.Object,
            _jwtSettings.Object)!;

        var handleMethod = handlerType.GetMethod("Handle")!;
        var task = (Task<BusinessResult<JwtTokenResponse>>)handleMethod.Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }

    private void ArrangeRotation(User user, string audience = CustomerAudience)
    {
        var record = RefreshTokenEntity.Create(
            userId: user.Id,
            tokenHash: "hash",
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: audience,
            deviceLabel: null,
            ipAddress: null);

        _refreshTokenService
            .Setup(s => s.RotateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssuedRefreshToken("new-raw-token", record));
        _userRepository
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    [Fact]
    public async Task RequiredProfile_Mismatch_Rejects_With_InvalidRefreshToken()
    {
        var demoted = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Employee });
        ArrangeRotation(demoted);

        var result = await Handle(new RefreshTokenCmd.Command(
            Token: "any", RequiredProfile: UserProfile.Customer, RequiredAudience: CustomerAudience));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidRefreshToken, result.Error!.Message);
    }

    [Fact]
    public async Task RequiredProfile_Match_Succeeds_With_New_Token()
    {
        var customer = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });
        ArrangeRotation(customer);

        var result = await Handle(new RefreshTokenCmd.Command(
            Token: "any", RequiredProfile: UserProfile.Customer, RequiredAudience: CustomerAudience));

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.Token));
        Assert.False(string.IsNullOrEmpty(result.Value.RefreshToken));
    }
}
