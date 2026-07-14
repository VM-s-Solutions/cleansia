using System.Reflection;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Devices;

/// <summary>
/// RegisterDevice is the push-registration entry point hit on every login. The
/// critical invariant: a device that was soft-deleted on a prior logout must be
/// RECLAIMED (reactivated) on the next login — never re-INSERTed — because
/// (UserId, DeviceId) is uniquely indexed across active AND inactive rows, so a
/// second INSERT would trip a Postgres unique violation and permanently break
/// push registration for that user+device.
/// </summary>
public class RegisterDeviceHandlerTests
{
    private const string CallerUserId = "caller-user-1";
    private const string DeviceId = "device-known-1";
    private const string OldToken = "token-old";
    private const string NewToken = "token-new";

    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public RegisterDeviceHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
    }

    private void ArrangeLookup(Device? device)
    {
        _deviceRepository
            .Setup(r => r.GetByUserAndDeviceIdIncludingInactiveAsync(CallerUserId, DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
    }

    private async Task<BusinessResult<RegisterDevice.Response>> InvokeHandler(string deviceId, string token, string platform)
    {
        var handlerType = typeof(RegisterDevice).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _deviceRepository.Object, _session.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<RegisterDevice.Response>>)handleMethod!.Invoke(
            handler, [new RegisterDevice.Command(deviceId, token, platform), CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Registering_A_Brand_New_Device_Inserts_It()
    {
        ArrangeLookup(null);
        Device? added = null;
        _deviceRepository.Setup(r => r.Add(It.IsAny<Device>())).Callback<Device>(d => added = d);

        var result = await InvokeHandler(DeviceId, NewToken, "ios");

        Assert.True(result.IsSuccess);
        _deviceRepository.Verify(r => r.Add(It.IsAny<Device>()), Times.Once);
        Assert.NotNull(added);
        Assert.Equal(added!.Id, result.Value.DeviceId);
        Assert.Equal(NewToken, added.DeviceToken);
        Assert.Equal("ios", added.Platform);
        Assert.True(added.IsActive);
    }

    [Fact]
    public async Task Re_Registering_An_Active_Device_Updates_The_Token_Without_Inserting()
    {
        var device = Device.Create(CallerUserId, "ios", OldToken, DeviceId);
        ArrangeLookup(device);

        var result = await InvokeHandler(DeviceId, NewToken, "ios");

        Assert.True(result.IsSuccess);
        Assert.Equal(device.Id, result.Value.DeviceId);
        Assert.Equal(NewToken, device.DeviceToken);
        Assert.True(device.IsActive);
        _deviceRepository.Verify(r => r.Add(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task Relogin_Reclaims_A_SoftDeleted_Device_Instead_Of_Inserting_A_Duplicate()
    {
        // Simulate the logout tombstone: the row still exists but IsActive=false.
        var device = Device.Create(CallerUserId, "ios", OldToken, DeviceId);
        device.IsActive = false;
        device.UpdateNotificationsEnabled(false);
        ArrangeLookup(device);

        var result = await InvokeHandler(DeviceId, NewToken, "ios");

        Assert.True(result.IsSuccess);
        Assert.Equal(device.Id, result.Value.DeviceId);
        // Reclaimed in place — reactivated, token refreshed, notifications re-enabled.
        Assert.True(device.IsActive);
        Assert.Equal(NewToken, device.DeviceToken);
        Assert.True(device.NotificationsEnabled);
        // Never a second INSERT — that is what would collide on the unique index.
        _deviceRepository.Verify(r => r.Add(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task A_Different_User_On_The_Same_Physical_Device_Gets_Its_Own_Row_Not_A_Reclaim()
    {
        // User A may already own a row for this deviceId (ANDROID_ID is shared across
        // accounts on one handset). The lookup is scoped by the SESSION user, so User B
        // must never see or reclaim User A's row — it inserts its own.
        const string otherUser = "caller-user-2";
        _session.Setup(s => s.GetUserId()).Returns(otherUser);
        ArrangeLookup(null); // no row for CallerUserId; and none arranged for otherUser
        Device? added = null;
        _deviceRepository.Setup(r => r.Add(It.IsAny<Device>())).Callback<Device>(d => added = d);

        var result = await InvokeHandler(DeviceId, NewToken, "ios");

        Assert.True(result.IsSuccess);
        _deviceRepository.Verify(
            r => r.GetByUserAndDeviceIdIncludingInactiveAsync(otherUser, DeviceId, It.IsAny<CancellationToken>()),
            Times.Once);
        _deviceRepository.Verify(r => r.Add(It.IsAny<Device>()), Times.Once);
        Assert.NotNull(added);
        Assert.Equal(otherUser, added!.UserId);
    }
}
