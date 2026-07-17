using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
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

/// <summary>
/// TC-REVOKE-NOW-4 (iii) - the refresh path is server-authoritative: the new access token's device_id
/// claim comes from the PERSISTED rotated record's DeviceId, never the request X-Device-Id header. A
/// rotated session's claim can never drift from the device id revocation matches on (ADR-0026 D1).
/// </summary>
public class RefreshTokenDeviceIdClaimTests
{
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IRequestMetadataProvider> _requestMetadata = new();
    private readonly Mock<IJwtSettings> _jwtSettings = new();

    public RefreshTokenDeviceIdClaimTests()
    {
        _jwtSettings.SetupGet(s => s.Secret).Returns(new string('k', 64));
        _jwtSettings.SetupGet(s => s.Issuer).Returns("cleansia.tests");
        _jwtSettings.SetupGet(s => s.AccessTokenExpMinutes).Returns(15);
    }

    [Fact]
    public async Task Refresh_mints_device_id_from_persisted_record_not_header()
    {
        var user = UserMockFactory.Generate(new UserMockFactory.UserPartial { Profile = UserProfile.Customer });

        var record = RefreshTokenEntity.Create(
            userId: user.Id,
            tokenHash: "hash",
            expiresAt: DateTimeOffset.UtcNow.AddDays(7),
            audience: JwtAudiences.Customer,
            deviceLabel: null,
            ipAddress: null,
            deviceId: "persisted-A");

        _refreshTokenService
            .Setup(s => s.RotateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(new IssuedRefreshToken("new-raw-token", record));
        _userRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        // A different (or hostile) header on the refresh request must be ignored.
        _requestMetadata.SetupGet(r => r.DeviceId).Returns("header-B");

        var result = await Handle(new RefreshTokenCmd.Command("any")
        {
            RequiredAudience = JwtAudiences.Customer,
        });

        Assert.True(result.IsSuccess);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token);
        var deviceClaim = jwt.Claims.SingleOrDefault(c => c.Type == AuthExtensions.DeviceIdClaimType);
        Assert.NotNull(deviceClaim);
        Assert.Equal("persisted-A", deviceClaim!.Value);
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
