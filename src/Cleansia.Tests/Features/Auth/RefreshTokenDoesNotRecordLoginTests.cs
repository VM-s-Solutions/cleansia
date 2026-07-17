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

public class RefreshTokenDoesNotRecordLoginTests
{
    private const string CustomerAudience = JwtAudiences.Customer;

    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly Mock<IJwtSettings> _jwtSettings = new();

    public RefreshTokenDoesNotRecordLoginTests()
    {
        _jwtSettings.SetupGet(s => s.Secret).Returns(new string('k', 64));
        _jwtSettings.SetupGet(s => s.Issuer).Returns("cleansia.tests");
        _jwtSettings.SetupGet(s => s.AccessTokenExpMinutes).Returns(15);
    }

    [Fact]
    public async Task Refresh_Does_Not_Touch_LastLoginAt()
    {
        var customer = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });

        var record = RefreshTokenEntity.Create(
            userId: customer.Id,
            tokenHash: "hash",
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: CustomerAudience,
            deviceLabel: null,
            ipAddress: null);

        _refreshTokenService
            .Setup(s => s.RotateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssuedRefreshToken("new-raw-token", record));
        _userRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);

        var result = await Handle(new RefreshTokenCmd.Command("any")
        {
            RequiredProfile = UserProfile.Customer,
            RequiredAudience = CustomerAudience,
        });

        Assert.True(result.IsSuccess);
        Assert.Null(customer.LastLoginAt);
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
            _jwtSettings.Object,
            TimeProvider.System)!;

        var handleMethod = handlerType.GetMethod("Handle")!;
        var task = (Task<BusinessResult<JwtTokenResponse>>)handleMethod.Invoke(handler, [command, CancellationToken.None])!;
        return await task;
    }
}
