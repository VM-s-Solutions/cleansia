using System.Reflection;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.AppServices.Features.Devices.DTOs;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Devices;

public class GetMyDevicesHandlerTests
{
    private const string CallerUserId = "user-A";
    private const string OtherUserId = "user-B";

    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public GetMyDevicesHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
    }

    private async Task<BusinessResult<IReadOnlyList<DeviceDto>>> InvokeHandler(string? currentDeviceId)
    {
        var handlerType = typeof(GetMyDevices).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _deviceRepository.Object, _session.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<IReadOnlyList<DeviceDto>>>)handleMethod!.Invoke(
            handler, [new GetMyDevices.Query(currentDeviceId), CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Returns_Only_The_Callers_Own_Devices()
    {
        var mine = Device.Create(CallerUserId, "android", "tok-1", "dev-1");
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync(CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([mine]);

        var result = await InvokeHandler(currentDeviceId: null);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.All(result.Value, d => Assert.Equal(mine.DeviceId, d.DeviceId));
        _deviceRepository.Verify(r => r.GetByUserIdAsync(CallerUserId, It.IsAny<CancellationToken>()), Times.Once);
        _deviceRepository.Verify(r => r.GetByUserIdAsync(OtherUserId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task User_A_Never_Sees_User_B_Devices_Because_Scope_Is_The_Jwt_UserId()
    {
        // Repository is asked ONLY for the caller's id (from the JWT, never from input).
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync(CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await InvokeHandler(currentDeviceId: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        _deviceRepository.Verify(
            r => r.GetByUserIdAsync(It.Is<string>(id => id != CallerUserId), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Marks_The_Current_Device_When_Caller_Reports_Its_Own_DeviceId()
    {
        var current = Device.Create(CallerUserId, "android", "tok-1", "dev-current");
        var other = Device.Create(CallerUserId, "ios", "tok-2", "dev-other");
        _deviceRepository
            .Setup(r => r.GetByUserIdAsync(CallerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([current, other]);

        var result = await InvokeHandler(currentDeviceId: "dev-current");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Single(d => d.DeviceId == "dev-current").IsCurrent);
        Assert.False(result.Value.Single(d => d.DeviceId == "dev-other").IsCurrent);
    }
}
