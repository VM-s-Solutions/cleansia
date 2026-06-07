using System.Reflection;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.Domain.Devices;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Devices;

public class UnregisterDeviceHandlerTests
{
    private const string CallerUserId = "caller-user-1";
    private const string KnownDeviceId = "device-known-1";
    private const string MissingDeviceId = "device-missing-2";

    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public UnregisterDeviceHandlerTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(CallerUserId);
    }

    private Device ArrangeDevice(string deviceId)
    {
        var device = Device.Create(CallerUserId, "android", "token-abc", deviceId);
        _deviceRepository
            .Setup(r => r.GetByUserAndDeviceIdAsync(CallerUserId, deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);
        return device;
    }

    private async Task<BusinessResult<UnregisterDevice.Response>> InvokeHandler(string deviceId)
    {
        var handlerType = typeof(UnregisterDevice).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _deviceRepository.Object, _session.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<UnregisterDevice.Response>>)handleMethod!.Invoke(
            handler, [new UnregisterDevice.Command(deviceId), CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task Unregistering_Existing_Device_Soft_Deletes_And_Never_Hard_Removes()
    {
        var device = ArrangeDevice(KnownDeviceId);

        var result = await InvokeHandler(KnownDeviceId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        _deviceRepository.Verify(r => r.Deactivate(device), Times.Once);
        _deviceRepository.Verify(r => r.Remove(It.IsAny<Device>()), Times.Never);
    }

    [Fact]
    public async Task Unregistering_Missing_Device_Is_A_Noop_And_Still_Succeeds()
    {
        var result = await InvokeHandler(MissingDeviceId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        _deviceRepository.Verify(r => r.Deactivate(It.IsAny<Device>()), Times.Never);
        _deviceRepository.Verify(r => r.Remove(It.IsAny<Device>()), Times.Never);
    }
}
