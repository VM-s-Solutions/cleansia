using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.LiveActivities;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Devices;

public class RevokeDeviceHandlerTests
{
    private const string CallerUserId = "user-A";
    private const string OwnedId = "row-owned-1";
    private const string ForeignId = "row-foreign-2";

    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IRefreshTokenService> _refreshTokenService = new();
    private readonly Mock<ILiveActivityTokenRepository> _liveActivityTokenRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public RevokeDeviceHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        _liveActivityTokenRepository
            .Setup(r => r.GetByUserAndDeviceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LiveActivityToken>());
    }

    private Device ArrangeOwnedDevice()
    {
        var device = Device.Create(CallerUserId, "android", "tok-1", "dev-1");
        typeof(Device).BaseType!.GetProperty("Id")!.SetValue(device, OwnedId);
        _deviceRepository
            .Setup(r => r.GetByIdAndUserAsync(OwnedId, CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        return device;
    }

    private async Task<BusinessResult<RevokeDevice.Response>> InvokeHandler(string deviceRowId)
    {
        var handlerType = typeof(RevokeDevice).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(
            handlerType!, _deviceRepository.Object, _refreshTokenService.Object, _liveActivityTokenRepository.Object, _session.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<RevokeDevice.Response>>)handleMethod!.Invoke(
            handler, [new RevokeDevice.Command(deviceRowId), CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Revoking_Own_Device_Deactivates_The_Row()
    {
        var device = ArrangeOwnedDevice();

        var result = await InvokeHandler(OwnedId);

        Assert.True(result.IsSuccess);
        _deviceRepository.Verify(r => r.Deactivate(device), Times.Once);
        _deviceRepository.Verify(r => r.Remove(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task Revoking_A_Device_I_Do_Not_Own_Returns_NotFound_And_Leaves_The_Row_Intact()
    {
        // The ownership-scoped lookup yields null for a foreign id — indistinguishable
        // from a non-existent id (S3: never reveals the row exists).
        _deviceRepository
            .Setup(r => r.GetByIdAndUserAsync(ForeignId, CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        var result = await InvokeHandler(ForeignId);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DeviceNotFound, result.Error!.Message);
        _deviceRepository.Verify(r => r.Deactivate(It.IsAny<Device>()), Times.Never);
        _deviceRepository.Verify(r => r.Remove(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task Revoking_Own_Device_Hard_Deletes_Its_Live_Activity_Tokens()
    {
        var device = ArrangeOwnedDevice();
        var tokens = new[]
        {
            LiveActivityToken.Create(CallerUserId, device.DeviceId, orderId: null, "push-to-start", tenantId: null),
            LiveActivityToken.Create(CallerUserId, device.DeviceId, "order-1", "update-tok", tenantId: null),
        };
        _liveActivityTokenRepository
            .Setup(r => r.GetByUserAndDeviceAsync(CallerUserId, device.DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokens);

        var result = await InvokeHandler(OwnedId);

        Assert.True(result.IsSuccess);
        _liveActivityTokenRepository.Verify(r => r.RemoveRange(tokens), Times.Once);
    }

    [Fact]
    public async Task Revoking_Own_Device_Also_Revokes_That_Devices_Refresh_Tokens()
    {
        var device = ArrangeOwnedDevice();

        var result = await InvokeHandler(OwnedId);

        Assert.True(result.IsSuccess);
        _refreshTokenService.Verify(
            s => s.RevokeByDeviceAsync(CallerUserId, device.DeviceId, "device_revoked", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Does_Not_Touch_Refresh_Tokens_When_The_Device_Is_Not_Found()
    {
        _deviceRepository
            .Setup(r => r.GetByIdAndUserAsync(ForeignId, CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Device?)null);

        await InvokeHandler(ForeignId);

        _refreshTokenService.Verify(
            s => s.RevokeByDeviceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
