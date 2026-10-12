using System.Reflection;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.LiveActivities;
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
    private readonly Mock<ILiveActivityTokenRepository> _liveActivityTokenRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public RegisterDeviceHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
        _deviceRepository
            .Setup(r => r.GetOtherUsersActiveByDeviceAndTokenIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Device>());
        _liveActivityTokenRepository
            .Setup(r => r.GetByUserAndDeviceIgnoringTenantAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LiveActivityToken>());
    }

    private void ArrangeLookup(Device? device)
    {
        _deviceRepository
            .Setup(r => r.GetByUserAndDeviceIdIncludingInactiveAsync(CallerUserId, DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
    }

    private async Task<BusinessResult<RegisterDevice.Response>> InvokeHandler(string deviceId, string? token, string platform)
    {
        var handlerType = typeof(RegisterDevice).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var dependencies = new Dictionary<Type, object>
        {
            [typeof(IDeviceRepository)] = _deviceRepository.Object,
            [typeof(ILiveActivityTokenRepository)] = _liveActivityTokenRepository.Object,
            [typeof(IUserSessionProvider)] = _session.Object,
        };
        var args = handlerType!.GetConstructors().Single().GetParameters().Select(p => dependencies[p.ParameterType]).ToArray();
        var handler = Activator.CreateInstance(handlerType!, args)!;
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
    public async Task A_TokenLess_Register_Inserts_The_Row_With_An_Empty_Token()
    {
        // Pre-APNs (or pre-permission) the client registers with no push token at all —
        // the row must still exist so the Devices page lists it and revocation can target
        // it; the dispatcher's IsNullOrEmpty filter keeps it out of push sends.
        ArrangeLookup(null);
        Device? added = null;
        _deviceRepository.Setup(r => r.Add(It.IsAny<Device>())).Callback<Device>(d => added = d);

        var result = await InvokeHandler(DeviceId, null, "ios");

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(string.Empty, added!.DeviceToken);
        Assert.True(added.IsActive);
    }

    [Fact]
    public async Task A_TokenLess_ReRegister_Keeps_The_Existing_Real_Token()
    {
        // An app relaunch before the push permission resolves re-registers token-less;
        // that must never wipe the real token a previous register stored.
        var device = Device.Create(CallerUserId, "ios", OldToken, DeviceId);
        ArrangeLookup(device);

        var result = await InvokeHandler(DeviceId, "  ", "ios");

        Assert.True(result.IsSuccess);
        Assert.Equal(OldToken, device.DeviceToken);
        Assert.True(device.IsActive);
        _deviceRepository.Verify(r => r.Add(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task A_Real_Token_Upgrades_A_TokenLess_Row()
    {
        // The APNs token arriving later must upgrade the placeholder row in place.
        var device = Device.Create(CallerUserId, "ios", string.Empty, DeviceId);
        ArrangeLookup(device);

        var result = await InvokeHandler(DeviceId, NewToken, "ios");

        Assert.True(result.IsSuccess);
        Assert.Equal(NewToken, device.DeviceToken);
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

    [Fact]
    public async Task Registering_Retires_Every_Other_Accounts_Active_Row_Holding_This_Handset_And_Token()
    {
        // A shared handset whose previous accounts' sign-outs never reached the server: their rows hold
        // this install's device id and push token, so their pushes would keep reaching the caller.
        var previousAccount = Device.Create("previous-user-1", "android", NewToken, DeviceId);
        var earlierAccount = Device.Create("previous-user-2", "android", NewToken, DeviceId);
        ArrangeLookup(null);
        _deviceRepository
            .Setup(r => r.GetOtherUsersActiveByDeviceAndTokenIgnoringTenantAsync(
                CallerUserId, DeviceId, NewToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync([previousAccount, earlierAccount]);

        var result = await InvokeHandler(DeviceId, NewToken, "android");

        Assert.True(result.IsSuccess);
        _deviceRepository.Verify(r => r.Deactivate(previousAccount), Times.Once);
        _deviceRepository.Verify(r => r.Deactivate(earlierAccount), Times.Once);
        _deviceRepository.Verify(r => r.Add(It.Is<Device>(d => d.UserId == CallerUserId)), Times.Once);
    }

    [Fact]
    public async Task Re_Registering_Its_Own_Row_Retires_Only_Other_Accounts_Rows()
    {
        var own = Device.Create(CallerUserId, "android", OldToken, DeviceId);
        var previousAccount = Device.Create("previous-user-1", "android", NewToken, DeviceId);
        ArrangeLookup(own);
        _deviceRepository
            .Setup(r => r.GetOtherUsersActiveByDeviceAndTokenIgnoringTenantAsync(
                CallerUserId, DeviceId, NewToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync([previousAccount]);

        var result = await InvokeHandler(DeviceId, NewToken, "android");

        Assert.True(result.IsSuccess);
        Assert.Equal(own.Id, result.Value.DeviceId);
        Assert.True(own.IsActive);
        Assert.Equal(NewToken, own.DeviceToken);
        _deviceRepository.Verify(r => r.Deactivate(previousAccount), Times.Once);
        _deviceRepository.Verify(r => r.Deactivate(own), Times.Never);
    }

    [Fact]
    public async Task Registering_Deletes_Each_Retired_Accounts_Live_Activity_Tokens_For_This_Install()
    {
        var previousAccount = Device.Create("previous-user-1", "ios", NewToken, DeviceId);
        var earlierAccount = Device.Create("previous-user-2", "ios", NewToken, DeviceId);
        IReadOnlyList<LiveActivityToken> previousAccountTokens =
        [
            LiveActivityToken.Create("previous-user-1", DeviceId, orderId: null, "push-to-start", tenantId: null),
            LiveActivityToken.Create("previous-user-1", DeviceId, "order-1", "update-order-1", tenantId: null),
        ];
        IReadOnlyList<LiveActivityToken> earlierAccountTokens =
        [
            LiveActivityToken.Create("previous-user-2", DeviceId, orderId: null, "push-to-start", tenantId: null),
        ];
        ArrangeLookup(null);
        _deviceRepository
            .Setup(r => r.GetOtherUsersActiveByDeviceAndTokenIgnoringTenantAsync(
                CallerUserId, DeviceId, NewToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync([previousAccount, earlierAccount]);
        _liveActivityTokenRepository
            .Setup(r => r.GetByUserAndDeviceIgnoringTenantAsync("previous-user-1", DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousAccountTokens);
        _liveActivityTokenRepository
            .Setup(r => r.GetByUserAndDeviceIgnoringTenantAsync("previous-user-2", DeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(earlierAccountTokens);

        var result = await InvokeHandler(DeviceId, NewToken, "ios");

        Assert.True(result.IsSuccess);
        _liveActivityTokenRepository.Verify(r => r.RemoveRange(previousAccountTokens), Times.Once);
        _liveActivityTokenRepository.Verify(r => r.RemoveRange(earlierAccountTokens), Times.Once);
        _liveActivityTokenRepository.Verify(
            r => r.GetByUserAndDeviceIgnoringTenantAsync(CallerUserId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_Registration_That_Retires_No_Row_Reads_And_Deletes_No_Live_Activity_Token()
    {
        ArrangeLookup(null);

        var result = await InvokeHandler(DeviceId, NewToken, "ios");

        Assert.True(result.IsSuccess);
        _liveActivityTokenRepository.Verify(
            r => r.GetByUserAndDeviceIgnoringTenantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _liveActivityTokenRepository.Verify(r => r.RemoveRange(It.IsAny<IEnumerable<LiveActivityToken>>()), Times.Never);
    }
}
