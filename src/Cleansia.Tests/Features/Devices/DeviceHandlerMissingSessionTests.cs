using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Devices;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Devices;

/// <summary>
/// IA-13 (handlers) — a missing session must surface as a canonical
/// <see cref="BusinessResult"/> failure with a <see cref="BusinessErrorMessage"/> key, NOT a bare
/// <see cref="UnauthorizedAccessException"/> with an inline English string. The repository is never
/// touched when there is no caller identity.
/// </summary>
public class DeviceHandlerMissingSessionTests
{
    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    public DeviceHandlerMissingSessionTests()
    {
        _session.Setup(s => s.GetUserId()).Returns((string?)null);
    }

    private async Task<BusinessResult<TResponse>> InvokeHandler<TResponse>(Type featureType, object command)
    {
        var handlerType = featureType.GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(handlerType);
        var handler = Activator.CreateInstance(handlerType!, _deviceRepository.Object, _session.Object)!;
        var handleMethod = handlerType!.GetMethod("Handle");
        Assert.NotNull(handleMethod);
        var task = (Task<BusinessResult<TResponse>>)handleMethod!.Invoke(
            handler, [command, CancellationToken.None])!;
        return await task;
    }

    [Fact]
    public async Task RegisterDevice_NoSession_ReturnsFailure_NotRawException()
    {
        var result = await InvokeHandler<RegisterDevice.Response>(
            typeof(RegisterDevice),
            new RegisterDevice.Command("device-1", "token-1", "android"));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error!.Message);
        _deviceRepository.Verify(
            r => r.GetByUserAndDeviceIdIncludingInactiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnregisterDevice_NoSession_ReturnsFailure_NotRawException()
    {
        var result = await InvokeHandler<UnregisterDevice.Response>(
            typeof(UnregisterDevice),
            new UnregisterDevice.Command("device-1"));

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.UserNotFound, result.Error!.Message);
        _deviceRepository.Verify(
            r => r.GetByUserAndDeviceIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
